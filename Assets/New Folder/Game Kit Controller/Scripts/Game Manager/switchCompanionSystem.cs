using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class switchCompanionSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool switchCompanionEnabled = true;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventToSetCharacterAsAI;

    [Space]

    public UnityEvent eventToSetCharacterAsPlayer;

    [Space]

    public UnityEvent eventToSetCharacterAsOnlyPlayerActive;

    [Space]

    public UnityEvent eventToSetCharacterAsPlayerNotActive;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController playerControllerManager;
    public playerCharactersManager mainPlayerCharactersManager;

    public friendListManager mainfriendListManager;


    public void activateEventToSetCharacterAsAI ()
    {
        eventToSetCharacterAsAI.Invoke ();
    }

    public void activateEventToSetCharacterAsPlayer ()
    {
        eventToSetCharacterAsPlayer.Invoke ();
    }

    public void activateEventToSetCharacterAsOnlyPlayerActive ()
    {
        eventToSetCharacterAsOnlyPlayerActive.Invoke ();
    }

    public void activateEventToSetCharacterAsPlayerNotActive ()
    {
        eventToSetCharacterAsPlayerNotActive.Invoke ();
    }

    public void inputSwitchToNextCharacter ()
    {
        if (!switchCompanionEnabled) {
            return;
        }

        if (!playerControllerManager.isPlayerMenuActive () &&
            (!playerControllerManager.isUsingDevice () || playerControllerManager.isPlayerDriving ()) &&
            !playerControllerManager.isGamePaused () &&
            playerControllerManager.canPlayerMove ()) {

            bool mainPlayerCharactersManagerLocated = mainPlayerCharactersManager != null;

            if (!mainPlayerCharactersManagerLocated) {
                mainPlayerCharactersManager = playerCharactersManager.Instance;
            }

            if (!mainPlayerCharactersManagerLocated) {
                mainPlayerCharactersManager = FindObjectOfType<playerCharactersManager> ();

                mainPlayerCharactersManager.getComponentInstanceOnApplicationPlaying ();

                mainPlayerCharactersManagerLocated = mainPlayerCharactersManager != null;
            }

            if (mainPlayerCharactersManagerLocated) {

                mainPlayerCharactersManager.inputSetNextCharacterToControl ();
            }
        }
    }
}
