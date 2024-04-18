using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;

/// <summary>
/// dictation mode is the mode where the PC tries to recognize the speech with out any assistnace or guidance. it is the most clear way.
/// 
/// Hypotethis are thrown super fast, but could have mistakes.
/// Resulted complete phrase will be determined once the person stops speaking. the best guess from the PC will go on the result.
/// 
/// added by shachar oz
/// </summary>
public class DictationEngine : MonoBehaviour
{
    private Text ResultedText;
    protected DictationRecognizer dictationRecognizer;
    public GameObject SearchInputField;
    private TMP_InputField inputField;
    private string textToPrint;
    [SerializeField] private GameObject SearchManager;
    private int flagFoundObjects;
    private bool isListening;

    /// <summary>
    /// Hypotethis are thrown super fast, but could have mistakes.
    /// </summary>
    /// <param name="text"></param>
    /// 
    void Start()
    {
        inputField = SearchInputField.GetComponent<TMP_InputField>();
        textToPrint = "";
        isListening = false;
    }
    private void DictationRecognizer_OnDictationHypothesis(string text)
    {
        Debug.LogFormat("Dictation hypothesis: {0}", text);
    }

    /// <summary>
    /// thrown when engine has some messages, that are not specifically errors
    /// </summary>
    /// <param name="completionCause"></param>
    private void DictationRecognizer_OnDictationComplete(DictationCompletionCause completionCause)
    {
        if (completionCause != DictationCompletionCause.Complete)
        {
            Debug.LogWarningFormat("Dictation completed unsuccessfully: {0}.", completionCause);


            switch (completionCause)
            {
                case DictationCompletionCause.TimeoutExceeded:
                    flagFoundObjects = 3;
                    SearchManager.GetComponent<SearchScript>().UpdateResultsButton(flagFoundObjects);
                    CloseDictationEngine();
                    break;
                case DictationCompletionCause.PauseLimitExceeded:
                    flagFoundObjects = 3;
                    SearchManager.GetComponent<SearchScript>().UpdateResultsButton(flagFoundObjects);
                    CloseDictationEngine();
                    break;

                case DictationCompletionCause.UnknownError:
                case DictationCompletionCause.AudioQualityFailure:
                case DictationCompletionCause.MicrophoneUnavailable:
                case DictationCompletionCause.NetworkFailure:
                    flagFoundObjects = 4;
                    SearchManager.GetComponent<SearchScript>().UpdateResultsButton(flagFoundObjects);
                    CloseDictationEngine();
                    break;

                case DictationCompletionCause.Canceled:
                    //happens when focus moved to another application
                    flagFoundObjects = 3;
                    SearchManager.GetComponent<SearchScript>().UpdateResultsButton(flagFoundObjects);
                    break;

                case DictationCompletionCause.Complete:
                    CloseDictationEngine();
                    break;
            }
        }
    }

    /// <summary>
    /// Resulted complete phrase will be determined once the person stops speaking. the best guess from the PC will go on the result.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="confidence"></param>
    private void DictationRecognizer_OnDictationResult(string text, ConfidenceLevel confidence)
    {
        if (ResultedText) ResultedText.text += text + "\n";
        textToPrint = text;
        CloseDictationEngine();
    }

    private void DictationRecognizer_OnDictationError(string error, int hresult)
    {
        Debug.LogErrorFormat("Dictation error: {0}; HResult = {1}.", error, hresult);
    }


    private void OnApplicationQuit()
    {
        CloseDictationEngine();
    }

    public void StartDictationEngine()
    {
        textToPrint = "";
        if (isListening == false) // quick verification there are no other current dictation sessions.
        {
            isListening = true;
            dictationRecognizer = new DictationRecognizer();
            dictationRecognizer.AutoSilenceTimeoutSeconds = 5;

            dictationRecognizer.DictationHypothesis += DictationRecognizer_OnDictationHypothesis;
            dictationRecognizer.DictationResult += DictationRecognizer_OnDictationResult;
            dictationRecognizer.DictationComplete += DictationRecognizer_OnDictationComplete;
            dictationRecognizer.DictationError += DictationRecognizer_OnDictationError;

            dictationRecognizer.Start();

            flagFoundObjects = 4; // Listening for input
            SearchManager.GetComponent<SearchScript>().UpdateResultsButton(flagFoundObjects);
        }
        
    }

    public void CloseDictationEngine()
    {
        Debug.Log("Result: " + textToPrint);
        inputField.text = textToPrint;
        flagFoundObjects = 0;
        isListening = false;
        if (dictationRecognizer != null)
        {
            dictationRecognizer.DictationHypothesis -= DictationRecognizer_OnDictationHypothesis;
            dictationRecognizer.DictationComplete -= DictationRecognizer_OnDictationComplete;
            dictationRecognizer.DictationResult -= DictationRecognizer_OnDictationResult;
            dictationRecognizer.DictationError -= DictationRecognizer_OnDictationError;

            if (dictationRecognizer.Status == SpeechSystemStatus.Running)
                dictationRecognizer.Stop();
            
            dictationRecognizer.Dispose();
        }
    }
}