using UnityEngine;
using TMPro;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
public class SearchScript : MonoBehaviour
{
    public GameObject ContentHolder;
    public List<GameObject> Elements = new();
    public GameObject SearchInputField;
    public GameObject PossibleResultButton;
    public GameObject ConfirmTeleportCanvas;
    private GameObject[] buttonsToDelete;
    private float ButtonHeightOffset;
    [SerializeField] private GameObject SearchViewport;
    [SerializeField] private GameObject buttonsParent;
    [Header("Button Functionality")]
    public bool HighlightBubble;
    public bool TeleportToBubble;
    [Header("Speech to Text")]
    public bool enableSpeechToText;
    [SerializeField] private bool enableDictationEngine;
    [SerializeField] private bool enableWhisperAI;
    [SerializeField] private Button SpeechButton;
    [SerializeField] private GameObject statusButton;
    private string SearchText;
    [SerializeField] private GameObject DictationEngine;
    private List<GameObject> matchingElements = new();
    [SerializeField] private GameObject WhisperAI;

    // Start is called before the first frame update
    void Start()
    {
        ButtonHeightOffset = SearchInputField.GetComponent<RectTransform>().rect.height; // it's the size of the input field so that the first button spawns beneath it

        if (!enableSpeechToText)
        {
            SpeechButton.gameObject.SetActive(false);
        }
        else
        {
            if (enableDictationEngine)
            {
                DictationEngine.SetActive(true);
                WhisperAI.SetActive(false);
                SpeechButton.onClick.AddListener(DictationEngine.GetComponent<DictationEngine>().StartDictationEngine);
            }
            else if (enableWhisperAI)
            {
                DictationEngine.SetActive(false);
                WhisperAI.SetActive(true);
                SpeechButton.onClick.AddListener(WhisperAI.GetComponent<PythonRunner>().RunPython);
            }
        }

        TextMeshProUGUI statusButtonText = statusButton.GetComponentInChildren<TextMeshProUGUI>();
        statusButtonText.fontSize = 8f;
        statusButtonText.fontStyle = FontStyles.Italic;


    }

    public void Search()
    {
        // Clearing variables and scene elements
        DeleteButtons(); // Search() is called every time there is a change in the inputField so for every new search, we need to destroy previous buttons and spawn new ones
        matchingElements.Clear();
        int flagFoundObjects;

        // get input text
        SearchText = SearchInputField.GetComponent<TMP_InputField>().text;
        int searchTextLength = SearchText.Length;
        foreach (GameObject element in Elements)
        {
            if (element.name.Length >= searchTextLength && searchTextLength >= 1) // if the text input is longer than the element name and consists of at least 1 character
            {
                string elementName = element.name.ToLower();
                if (elementName.Contains(SearchText.ToLower()))
                {
                    matchingElements.Add(element);
                }
            }
        }

        int flag = matchingElements.Count > 0 ? 1 : 2;
        flagFoundObjects = searchTextLength >= 1 ? flag : 0;
        CreateButtons();
        UpdateResultsButton(flagFoundObjects);
    }

    public void DeleteButtons()
    {
        buttonsToDelete = GameObject.FindGameObjectsWithTag("TeleportButton");
        for (int i = 0; i < buttonsToDelete.Length; i++)
        {
            Destroy(buttonsToDelete[i]);
        }
    }

    private void CreateButtons()
    {
        for (int i = 0; i < matchingElements.Count; i++)
        {
            // Button instantiation and placement in the scene
            GameObject result = Instantiate(PossibleResultButton, buttonsParent.transform);
            result.tag = "TeleportButton";

            // finding start Index for matching text
            string elementLowerName = matchingElements[i].name.ToLower();
            int startIndex = elementLowerName.IndexOf(SearchText.ToLower());
            TextMeshProUGUI buttonText = result.GetComponentInChildren<TextMeshProUGUI>();
            if (startIndex != -1)
            {
                // highlighting matching text
                string highlightedText = matchingElements[i].name.Insert(startIndex + SearchText.Length, "</mark>");
                highlightedText = highlightedText.Insert(startIndex, "<mark=#179C7Daa>");
                buttonText.text = highlightedText;
            }
            else
            {
                buttonText.text = matchingElements[i].name;
            }


            // buttonText.fontSize = 10f;

            // Setup of all the information inside the button to connect it to the correct bubble and to manage the interaction with the corresponding UI.
            TeleportOnSearch buttonArguments = result.GetComponent<TeleportOnSearch>();
            buttonArguments.BubbleTeleportAnchor = matchingElements[i].GetComponent<Bubble>().VRBubbleTeleport;
            buttonArguments.player = GameObject.FindGameObjectWithTag("Player");
            buttonArguments.BubblePosition = matchingElements[i].transform.position;
            buttonArguments.ConfirmationCanvas = ConfirmTeleportCanvas;
            buttonArguments.SearchInputField = SearchInputField;
            buttonArguments.BubbleName = matchingElements[i].name;
            buttonArguments.BubbleMenuManager = matchingElements[i].GetComponent<Bubble>().BubbleMenuManager;
            if (HighlightBubble && !TeleportToBubble)
            {
                result.GetComponent<Button>().onClick.AddListener(buttonArguments.HighlightBubble);
            }
            else if (!HighlightBubble && TeleportToBubble)
            {
                result.GetComponent<Button>().onClick.AddListener(buttonArguments.ShowConfirmationCanvas);
            }
        }

    }

    public void ResetSearch(bool tryAgain = false)
    {
        SearchInputField.GetComponent<TMP_InputField>().text = "";
        statusButton.gameObject.SetActive(false);
        if (tryAgain)
        {
            if (DictationEngine.gameObject.activeSelf)
            {
                // case 3 leads to restarting the voice recognition
                DictationEngine.GetComponent<DictationEngine>().StartDictationEngine();
            }
        }
    }

    public void UpdateResultsButton(int flagFoundObjects)
    {
        TextMeshProUGUI statusButtonText = statusButton.GetComponentInChildren<TextMeshProUGUI>();
        //statusButton.onClick.AddListener(() => ResetSearch(false));
        switch (flagFoundObjects)
        {
            case 0: // no input in the field
                statusButton.gameObject.SetActive(false);
                break;
            case 1: // results found
                break;
            case 2: // input detected, no results found
                statusButton.gameObject.SetActive(true);
                statusButtonText.text = "No results found for ' " + SearchText;
                break;
            case 3: // timeout exceeded, try again
                statusButton.gameObject.SetActive(true);
                statusButtonText.text = "No input detected - Try again";
                break;
            case 4: // voice recognition activated, waiting for input
                statusButton.gameObject.SetActive(true);
                statusButtonText.text = "Listening for input";
                break;
            case 5:
                statusButton.gameObject.SetActive(true);
                GetComponent<CountdownScript>().StartCountdown();
                break;
            case 6:
                statusButton.gameObject.SetActive(true);
                statusButtonText.text = "Processing information";
                break;
            default:
                break;
        }
    }
}