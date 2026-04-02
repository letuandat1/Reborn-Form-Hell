using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class Player_Animate : MonoBehaviour
{
    [GetComponent] private Animator ani;
    [GetComponent] private Player_StateManager sm;
    [SerializeFieldExternal][SerializeField] private AnimationStateMapper aniSM;
    [SerializeField] private Transform AudioManagerTransform; // Reference to the AudioManager Transform
    private PlayerAudioManager audioManager;
    // public bool isPlayingKnifeSound = false; // Flag to check if the knife sound is currently playing
    // Start is called before the first frame update
    void Start()
    {
        ani = GetComponent<Animator>();
        sm = GetComponent<Player_StateManager>();
        audioManager = AudioManagerTransform.GetComponent<PlayerAudioManager>();
    }


    // Update is called once per frame
    void Update()
    {
        if (sm.ShouldForceIdleAnimation)
        {
            ani.Play(aniSM[PlayerAnimationKeys.Idle]);
            return; // Force idle, ignore all other animations
        }

        if (sm.IsGunDrawing)
        {
            ani.Play(aniSM[PlayerAnimationKeys.GunDraw]);
            return;
        }
        if (sm.IsKnifeUsing)
        {
            ani.Play(aniSM[PlayerAnimationKeys.KnifeUse]);
            return;
        }
        if (sm.IsRunStarting)
        {
            ani.Play(aniSM[PlayerAnimationKeys.RunStart]);

            return;
        }
        if (sm.IsRunLooping)
        {
            ani.Play(aniSM[PlayerAnimationKeys.RunLoop]);
            audioManager.PlaySFX(audioManager.PantingClip);
            StartCoroutine(PlayPantingSound());

            return;
        }
        if (sm.IsWalking)
        {
            ani.Play(aniSM[PlayerAnimationKeys.Walk]);
            return;
        }
        ani.Play(aniSM[PlayerAnimationKeys.Idle]);
    }
    // làm tiếng panting khi hồi sức tiếng càng nhỏ dần
    private IEnumerator PlayPantingSound()
    {
        float volume = 1.0f;
        while (volume > 0)
        {
            audioManager.PlaySFX(audioManager.PantingClip);
            volume -= 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
    }
    public void PlayKnifeSound()
    {
        if (audioManager.GetCurrentClipName() != audioManager.MeleeAttackClip.name)
        {
            audioManager.PlaySFX(audioManager.MeleeAttackClip);
        }
    }
    public void PlayGunShotSound()
    {
        if (audioManager.GetCurrentClipName() != audioManager.GunShotClip.name)
        {
            audioManager.PlaySFX(audioManager.GunShotClip);
        }
    }

}
