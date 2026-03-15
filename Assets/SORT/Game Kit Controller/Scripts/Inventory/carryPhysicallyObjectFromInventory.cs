using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class carryPhysicallyObjectFromInventory : objectOnInventory
{
    [Header ("Main Settings")]
    [Space]

    public grabPhysicalObjectSystem mainGrabPhysicalObjectSystem;


    public override void carryPhysicalObjectFromInventory (GameObject currentPlayer)
    {
        //check if player is already carrying a different physical object, to remove it properly
        //check also if that object is an inventory object, to send it back to the inventory

        mainGrabPhysicalObjectSystem.setCurrentPlayer (currentPlayer);

        grabObjects currentGrabObjects = currentPlayer.GetComponent<grabObjects> ();

        if (currentGrabObjects != null) {
            bool isCarryingPhysicalObject = currentGrabObjects.isCarryingPhysicalObject ();

            bool isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled = false;

            GameObject currentObjectGrabbed = null;

            playerComponentsManager currentPlayerComponentsManager = currentPlayer.GetComponent<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {
                isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled = currentPlayerComponentsManager.getInventoryManager ().isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled ();
            }

            if (isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled) {
                currentObjectGrabbed = currentGrabObjects.getCurrentPhysicalObjectGrabbed ();

                if (currentObjectGrabbed != null) {
                    if (currentObjectGrabbed.GetComponentInChildren<objectOnInventory> () == null) {
                        currentObjectGrabbed = null;
                    }
                }
            }

            if (isCarryingPhysicalObject) {
                currentGrabObjects.checkIfDropObjectIfPhysical ();

                currentGrabObjects.grabPhysicalObjectExternally (gameObject);
            } else {
                currentGrabObjects.getClosestPhysicalObjectToGrab ();

                currentGrabObjects.grabObject ();
            }

            if (isSendGrabbedObjectToInventoryIfGrabbingNewOneEnabled) {
                if (currentObjectGrabbed != null) {
                    pickUpObject currentPickupObject = currentObjectGrabbed.GetComponent<pickUpObject> ();

                    if (currentPickupObject != null) {
                        currentPickupObject.pickObjectAfterXWait (currentPlayer);
                    }
                }
            }
        }
    }
}
