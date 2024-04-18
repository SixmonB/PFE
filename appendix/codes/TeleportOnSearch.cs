using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;

public class TeleportOnSearch : MonoBehaviour
{
    private FadeCanvas fadeCanvas = null;
    public GameObject BubbleTeleportAnchor;
    public GameObject player;
    public GameObject ConfirmationCanvas;
    public Vector3 BubblePosition;
    public GameObject SearchInputField;
    public string BubbleName;
    public GameObject BubbleMenuManager;

    private TeleportationProvider teleportationProvider;
    private VR_TeleportAnchorWithFade VRBubbleTeleport = null;
    private GameObject defaultTeleportationTarget = null;

    void Start()
    {
        fadeCanvas = FindObjectOfType<FadeCanvas>();
    }
    public void ShowConfirmationCanvas()
    {
        // When the confirmation canvas is spawned, we set up the references from the "yes" button to the selected bubble
        ConfirmationCanvas.SetActive(true);
        ConfirmationCanvas.GetComponent<ConfigureButtons>().SetReferences(gameObject);
    }
    public void ConfirmDecision()
    {
        Teleport();
        ConfirmationCanvas.SetActive(false);
        // To call for the CloseAll function and actually delete all buttons, we need to have the viewport activated.
        SearchInputField.GetComponent<ShowKeyboard>().CloseAll();
    }

    public void DenyDecision()
    {
        // The "no" button doesnt destroy the previous search and buttons, so we just enable the viewport.
        ConfirmationCanvas.SetActive(false);
    }
    private void Teleport()
    {
        SearchInputField.GetComponent<ShowKeyboard>().CloseAll();


        if (VRBubbleTeleport == null)
        {
            VRBubbleTeleport = GameObject.Find(BubbleName).GetComponentInChildren<VR_TeleportAnchorWithFade>();
        }

        if (defaultTeleportationTarget == null)
        {
            defaultTeleportationTarget = VRBubbleTeleport.gameObject.transform.Find("Default Teleport Destination").gameObject;
        }

        if (teleportationProvider == null)
        {
            teleportationProvider = FindObjectOfType<TeleportationProvider>();
        }

        TeleportRequest teleportRequest = new TeleportRequest
        {
            requestTime = Time.time,
            destinationPosition = defaultTeleportationTarget.transform.position,
            destinationRotation = defaultTeleportationTarget.transform.rotation
        };

        StartCoroutine(FadeSequence(teleportRequest));
    }




    public void HighlightBubble()
    {
        BubbleMenuManager.GetComponent<BubbleMenuManager>().ToggleHighlightBubble();
        BubbleMenuManager.GetComponent<BubbleMenuManager>().ToggleHighlightConnectors();
    }
}
