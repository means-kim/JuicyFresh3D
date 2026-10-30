using UnityEngine;

public class BossChargeStartBehaviour_k : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!animator.TryGetComponent(out BossChargeArea_k chargeArea) || !chargeArea.isActiveAndEnabled)
        {
            return;
        }

        chargeArea.PrepareChargeArea();
    }
}
