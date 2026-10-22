"""Create an in-place FBX derivative without changing the vendor animation."""
import hashlib
import json
import pathlib
import re
import struct
import uuid
import zlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/Zectorlab/BullTank/Art/Animations/AS_BullTank_Walk_F.FBX'
OUTPUT = ROOT / 'Assets/07.Animations/KwnagHee/BullTank_Walk_F_InPlace.fbx'
CONTROLLER = ROOT / 'Assets/07.Animations/KwnagHee/BossMonsterAnimator.controller'


class Fbx:
    def __init__(self, data):
        self.data = data
        self.wide = struct.unpack_from('<I', data, 23)[0] >= 7500
        self.header_size = 25 if self.wide else 13
        self.pos = 27
        self.nodes = []
        while True:
            node = self.read_node()
            if node is None:
                break
            self.nodes.append(node)
        self.footer = data[self.pos:]

    def read_node(self):
        start = self.pos
        end, count, _ = struct.unpack_from('<QQQ' if self.wide else '<III', self.data, start)
        self.pos += self.header_size - 1
        name_size = self.data[self.pos]
        self.pos += 1
        if end == 0:
            return None
        name = self.data[self.pos:self.pos + name_size].decode()
        self.pos += name_size
        props = []
        for _ in range(count):
            prop_start = self.pos
            kind = chr(self.data[self.pos])
            self.pos += 1
            if kind in 'YCIFDL':
                fmt = {'Y': 'h', 'C': '?', 'I': 'i', 'F': 'f', 'D': 'd', 'L': 'q'}[kind]
                value = struct.unpack_from('<' + fmt, self.data, self.pos)[0]
                self.pos += struct.calcsize(fmt)
            elif kind in 'SR':
                size = struct.unpack_from('<I', self.data, self.pos)[0]
                self.pos += 4
                value = self.data[self.pos:self.pos + size]
                self.pos += size
                if kind == 'S':
                    value = value.decode(errors='replace')
            else:
                length, encoding, size = struct.unpack_from('<III', self.data, self.pos)
                self.pos += 12
                raw = self.data[self.pos:self.pos + size]
                self.pos += size
                if encoding:
                    raw = zlib.decompress(raw)
                fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': '?', 'c': 'b'}[kind]
                value = struct.unpack('<' + str(length) + fmt, raw)
            props.append({'kind': kind, 'value': value, 'raw': self.data[prop_start:self.pos]})
        children = []
        terminated = False
        while self.pos < end:
            child = self.read_node()
            if child is None:
                terminated = True
                break
            children.append(child)
        assert self.pos == end, (name, self.pos, end)
        return {'name': name, 'props': props, 'children': children, 'terminated': terminated}

    def encode_node(self, node, offset):
        name = node['name'].encode()
        properties = b''.join(p['raw'] for p in node['props'])
        cursor = offset + self.header_size + len(name) + len(properties)
        children = b''
        for child in node['children']:
            encoded = self.encode_node(child, cursor)
            children += encoded
            cursor += len(encoded)
        if node['terminated']:
            children += bytes(self.header_size)
            cursor += self.header_size
        header = struct.pack('<QQQB' if self.wide else '<IIIB', cursor, len(node['props']), len(properties), len(name))
        return header + name + properties + children

    def encode(self):
        result = self.data[:27]
        for node in self.nodes:
            result += self.encode_node(node, len(result))
        return result + bytes(self.header_size) + self.footer


def values(node):
    return [p['value'] for p in node['props']]


def signature(node):
    return (node['name'], [p['raw'].hex() for p in node['props']], [signature(c) for c in node['children']])


def main():
    source_bytes = SOURCE.read_bytes()
    fbx = Fbx(source_bytes)
    assert fbx.encode() == source_bytes, 'Lossless FBX round-trip failed'
    objects = next(n for n in fbx.nodes if n['name'] == 'Objects')['children']
    connections = next(n for n in fbx.nodes if n['name'] == 'Connections')['children']
    by_id = {values(n)[0]: n for n in objects if n['props']}
    root_id = next(values(n)[0] for n in objects if n['name'] == 'Model' and values(n)[1].split('\0')[0] == 'DeformationSystem')
    links = [values(n) for n in connections if n['name'] == 'C']
    translation_nodes = {p[1] for p in links if p[0] == 'OP' and p[2] == root_id and p[3] == 'Lcl Translation'}
    changes = []
    for link in links:
        if link[0] != 'OP' or link[2] not in translation_nodes or link[3] not in ('d|X', 'd|Y'):
            continue
        curve = by_id[link[1]]
        key_values = next(n for n in curve['children'] if n['name'] == 'KeyValueFloat')
        prop = key_values['props'][0]
        previous = prop['value']
        constant = tuple(previous[0] for _ in previous)
        raw = struct.pack('<' + str(len(constant)) + 'f', *constant)
        compressed = zlib.compress(raw)
        prop['value'] = constant
        prop['raw'] = b'f' + struct.pack('<III', len(constant), 1, len(compressed)) + compressed
        # Keep interpolation flat as well, including authored cubic tangents.
        tangent_node = next((n for n in curve['children'] if n['name'] == 'KeyAttrDataFloat'), None)
        if tangent_node is not None:
            tangent_prop = tangent_node['props'][0]
            tangents = list(tangent_prop['value'])
            for index in range(0, len(tangents), 4):
                tangents[index] = 0.0
                tangents[index + 1] = 0.0
            tangent_raw = struct.pack('<' + str(len(tangents)) + 'f', *tangents)
            tangent_compressed = zlib.compress(tangent_raw)
            tangent_prop['value'] = tuple(tangents)
            tangent_prop['raw'] = b'f' + struct.pack('<III', len(tangents), 1, len(tangent_compressed)) + tangent_compressed
        changes.append({'axis': link[3], 'keys': len(previous), 'original_delta': previous[-1] - previous[0], 'new_delta': 0.0})
    assert len(changes) == 2, changes
    generated = fbx.encode()
    reparsed = Fbx(generated)
    assert [signature(n) for n in reparsed.nodes] == [signature(n) for n in fbx.nodes]
    OUTPUT.write_bytes(generated)
    meta = SOURCE.with_suffix(SOURCE.suffix + '.meta').read_text(encoding='utf-8-sig')
    meta = re.sub(r'(?ms)^AssetOrigin:\n.*?(?=^ModelImporter:)', '', meta)
    output_meta = OUTPUT.with_suffix(OUTPUT.suffix + '.meta')
    new_guid = re.search(r'^guid: (\w+)', output_meta.read_text(), re.M).group(1) if output_meta.exists() else uuid.uuid4().hex
    old_guid = re.search(r'^guid: (\w+)', meta, re.M).group(1)
    meta = re.sub(r'(?m)^guid: \w+', 'guid: ' + new_guid, meta, count=1)
    output_meta.write_text(meta, encoding='utf-8')
    controller = CONTROLLER.read_text(encoding='utf-8-sig')
    pattern = r'(?ms)(^--- !u!1102 &[^\n]+\nAnimatorState:\n(?:(?!^--- !u!).)*?^  m_Name: Boss_Walk\n)(.*?)(?=^--- !u!|\Z)'
    match = re.search(pattern, controller)
    assert match and (old_guid in match.group(2) or new_guid in match.group(2)), 'Boss_Walk does not reference the expected clip'
    new_block = match.group(0).replace(old_guid, new_guid)
    CONTROLLER.write_text(controller[:match.start()] + new_block + controller[match.end():], encoding='utf-8')
    assert SOURCE.read_bytes() == source_bytes
    report = {'source_unchanged_sha256': hashlib.sha256(source_bytes).hexdigest(), 'output': str(OUTPUT), 'guid': new_guid, 'changed_curves': changes, 'verified': ['source byte-identical', 'binary parser/writer lossless round-trip', 'generated FBX reparses identically', 'only planar root curve values changed', 'Boss_Walk motion reference updated']}
    (ROOT / 'tmp/bulltank_walk_fix_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
