# 프로젝트 작업 규칙

이 지침은 이 저장소 전체에 적용한다.

## 클래스 이름

- 프로젝트에서 작성하는 C# 클래스는 PascalCase 이름 뒤에 `_s`를 붙인다. 예: `Health_s`, `PlayerAttack_s`.
- `_s`는 사용자 지정 프로젝트 예외이며 `.editorconfig`의 타입명 규칙보다 우선한다. 이 예외는 이 문서에만 기록하고, 이를 맞추기 위해 `.editorconfig`를 변경하지 않는다.
- 검사에서 `_s`로 인한 이름 경고가 발생하면 프로젝트 예외와 검사 설정의 차이로 보고한다. 접미사를 제거하거나 검사 설정을 임의로 변경하지 않는다.
- Unity 스크립트 파일명은 클래스 이름과 일치시킨다. 예: `Health_s.cs`.
- struct, interface, enum에는 `_s`를 강제하지 않으며 기존 PascalCase 규칙을 따른다.
- 외부 에셋, 패키지, Unity 자동 생성 코드는 이 규칙을 적용하기 위해 수정하지 않는다.
- 기존 Unity 스크립트를 이동하거나 이름을 변경할 때는 `.meta` GUID와 씬·프리팹 참조를 보존한다.

## 코드 작성 및 검증

- 작업 전에 `.editorconfig`, `.husky/pre-commit`, `.husky/commit-msg`를 확인한다.
- `Assets/02.Scripts/`의 코드는 공백 4칸으로 들여쓰고, 여는 중괄호를 새 줄에 배치한다.
- 단일 행 조기 반환은 중괄호 없이 허용하지만 여러 줄 조건문에는 중괄호를 사용한다.
- 빈 줄에 불필요한 공백을 남기지 않는다.
- private 필드는 `_camelCase`, private const와 private static readonly 필드는 PascalCase, 지역 변수는 camelCase를 사용한다.
- 코드 수정 후 Husky와 같은 검사를 실행한다:

  `dotnet format JuicyFresh3D.sln --verify-no-changes --severity warn --include Assets/02.Scripts/`

- 복원 등 환경 문제로 기본 검사가 실패하면 원인을 보고한다. `--no-restore`로 재검사한 경우에도 이를 구분해서 알리고 기본 검사가 통과했다고 주장하지 않는다.
- Unity 컴파일·동작 확인과 코드 스타일 검사를 별도로 판단한다. 실행하지 못한 검증은 명시한다.
- 관련 없는 사용자 변경을 되돌리지 않는다.

## 커밋

- 커밋할 때 `.husky/commit-msg`가 허용하는 타입으로 `type: 설명` 또는 `type(scope): 설명` 형식의 제목을 작성한다.
- 제목 외에 비어 있지 않은 본문을 최소 한 줄 작성한다.
- 훅을 우회하지 않는다.
