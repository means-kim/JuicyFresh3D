using System;
using System.Collections.Generic;

public enum EffectToggleType_m
{
    // 회피
    DodgeSlowMotion,
    DodgeOutline,
    DodgeLightParticle,
    DodgeCrossFlash,
    DodgeVignette,
    DodgeDepthOfField,
    DodgeChromaticAberration,

    // 피격
    HitOutline,
    HitCameraShake,

    // 빈사
    NearDeathCameraShake,
    NearDeathVignette,
    NearDeathScreenCrack,

    // 특수공격
    SpecialAttackSword,

    // 보스 피격
    BossHitOutline,
    BossHitFlash,
    BossHitCameraShake,
}

// 연출별 on/off 상태입니다. 공유 컴포넌트가 있어 컴포넌트를 끄지 않고 호출 지점에서 확인합니다.
public static class EffectToggleSettings_m
{
    private static readonly HashSet<EffectToggleType_m> Disabled = new();

    public static event Action<EffectToggleType_m, bool> Changed;

    public static bool IsOn(EffectToggleType_m type)
    {
        return !Disabled.Contains(type);
    }

    public static void Set(EffectToggleType_m type, bool isOn)
    {
        bool changed = isOn ? Disabled.Remove(type) : Disabled.Add(type);
        if (changed)
        {
            Changed?.Invoke(type, isOn);
        }
    }
}
