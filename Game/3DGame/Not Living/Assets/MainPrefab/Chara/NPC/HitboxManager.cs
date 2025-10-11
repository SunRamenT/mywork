using UnityEngine;
using System.Collections.Generic;


public class HitboxManager : MonoBehaviour
{
    [Tooltip("攻撃の種類ごとの当たり判定グループ")]
    public List<AttackHitboxGroup> attackGroups;

    // アニメーションイベントから呼び出す（文字列の引数を追加）
    public void EnableHitboxes(string name)
    {
        foreach (var group in attackGroups)
        {
            if (group.attackName == name)
            {
                foreach (var hitbox in group.hitboxes)
                {
                    if (hitbox != null)
                    {
                        hitbox.enabled = true;
                    }
                }
                return;
            }
        }
    }

    // アニメーションイベントから呼び出す（文字列の引数を追加）
    public void DisableHitboxes(string name)
    {
        foreach (var group in attackGroups)
        {
            if (group.attackName == name)
            {
                foreach (var hitbox in group.hitboxes)
                {
                    if (hitbox != null)
                    {
                        hitbox.enabled = false;
                    }
                }
                return;
            }
        }
    }
}