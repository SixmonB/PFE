using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.IO;
using System;
using TMPro;
using UnityEngine.EventSystems;
using System.Linq;
using Unity.Netcode;

[RequireComponent(typeof(EventTrigger))]
public class OntologyManager : MonoBehaviour
{
    public static OntologyManager Instance { get; private set; }
    public BubblesDataContainer bubblesMainDataContainer;

    // Names of all ontologies appearing in the scene. Add to it when creating a new ontology, but check first if the specific ontology isn't on the list already.
    public List<string> ListOfOntologiesNames { get; set; } = new List<string>();

    // Used for storing and managing the saved JSON files
    public static List<string> savedJSONFiles = new List<string>();
    public GameObject selectedLoadItem { get; set; }
    public GameObject defaultLoadItem;
    

    [SerializeField] private GameObject bubbleClassPrefab;
    [SerializeField] private GameObject bubbleInstancePrefab;
    [SerializeField] private GameObject bubblePropertyPrefab;
    [SerializeField] private GameObject parentObject;
    [SerializeField] private GameObject SearchManager;
    [SerializeField] private GameObject UIManager;
    [SerializeField] private GameObject Parser;
    [SerializeField] private GameObject ontologyContainerPrefab;
    [SerializeField] private GameObject ontologyPrefab;

    [SerializeField] string directoryName;

    [Header("DevTools")]
    [Tooltip("This is the path to spawn an Ontology from the Test-Buttons!")]
    [SerializeField] private String dev_folderName;
    [SerializeField] private bool dev_isImporting = false;


    //Creates a ImportMenu to import ontology
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(200, 10, 150, 300));
        ImportButtons();
        GUILayout.EndArea();
    }

    void ImportButtons()
    {
        if (GUILayout.Button("Import Ontology"))
        {
            LoadOntologyFromDevButton();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        bubblesMainDataContainer = new BubblesDataContainer
        {
            bubbleDataList = new List<BubbleDataSO>()
        };
    }

    public void AddDataToMainDataContainer(BubbleDataSO data)
    {
        Instance.bubblesMainDataContainer.bubbleDataList.Add(data);
    }

    public void RemoveDataFromMainDataContainer(BubbleDataSO data)
    {
        Instance.bubblesMainDataContainer.bubbleDataList.Remove(data);
    }

    public void AddOntologyNameToList(BubbleDataSO data)
    {
        if (ListOfOntologiesNames.Contains(data.GetOntologyTag())) return;
        ListOfOntologiesNames.Add(data.GetOntologyTag());
    }

    public void RemoveOntologyNameFromList(BubbleDataSO data)
    {
        int occurrences = Instance.bubblesMainDataContainer.bubbleDataList.Count(x => x.GetOntologyTag() == data.GetOntologyTag());

        if (occurrences > 1) return;
        else ListOfOntologiesNames.Remove(data.GetOntologyTag());
    }

    /// <summary>
    /// Saves the ontology data to meta- and positioning files
    /// </summary>
    public string SaveOntologyMetaDataToFile()
    {
        try
        {
            ExtractedBubbleDataWrapper extractedBubbleDataWrapper = new ExtractedBubbleDataWrapper();

            foreach (BubbleDataSO dataSO in Instance.bubblesMainDataContainer.bubbleDataList)
            {
                extractedBubbleDataWrapper.extractedBubbleDatasList.Add(new ExtractedBubbleData(dataSO));
            }

            string json = JsonUtility.ToJson(extractedBubbleDataWrapper, true);
            string creationTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            string path = Path.Combine(Application.dataPath, "Saves", creationTime);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Saves", creationTime));
            }

            string fullFilePath = Path.Combine(path, "savedOntology_" + creationTime + ".json");
            File.WriteAllText(fullFilePath, json);

            // Add to the list of saved files
            savedJSONFiles.Add(fullFilePath);

            Debug.Log("Meta data saved to a file: " + fullFilePath);

            string fileDirectoryAndTime = path + "," + creationTime;

            string fileName = Path.Combine("exportedOntology_" + creationTime + ".jsonld");
            Parser.GetComponent<Parser>().ParseToJsonLD(fullFilePath, path, fileName);
            return fileDirectoryAndTime;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            Debug.LogError("Meta Data save failed.");
            return null;
        }
    }

    //Dev_Method to import Ontolgies using a GUI-Button
    public void LoadOntologyFromDevButton()
    {
        dev_isImporting = true;
        LoadAllOntologyDataFromFiles();
    }

    /// <summary>
    /// Loads the ontology data from meta- and positioning files
    /// </summary>
    public void LoadAllOntologyDataFromFiles()
    {
        if (selectedLoadItem != null || dev_isImporting)
        {
            string prefix = "savedOntology_";
            string extension = ".json";
            if (dev_isImporting)
            {
                directoryName = dev_folderName;
            }
            else
            {
                directoryName = selectedLoadItem.gameObject.GetComponentInChildren<TextMeshProUGUI>().text;
            }
            string fullFilePath = Path.Combine(Application.dataPath, "Saves", directoryName, prefix + directoryName + extension);
            dev_isImporting = false;
            Debug.Log($"FullFilePath: {fullFilePath}");

            if (File.Exists(fullFilePath))
            {
                string json = File.ReadAllText(fullFilePath);
                ExtractedBubbleDataWrapper extractedBubbleDataWrapper = JsonUtility.FromJson<ExtractedBubbleDataWrapper>(json);

                Dictionary<string, Bubble> bubblesToRelateToEachOther = new();

                SearchScript searchScript = SearchManager.GetComponent<SearchScript>();
                if (searchScript.Elements != null)
                {
                    searchScript.Elements.Clear();
                }

                // Instantiate bubbles and populate their properties
                InstantiateAndPopulateBubbles(extractedBubbleDataWrapper, bubblesToRelateToEachOther);

                // Establish relationships between bubbles - THE CULRPIT
                EstablishBubbleRelationships(extractedBubbleDataWrapper, bubblesToRelateToEachOther);

                // Load position and level
                LayoutManager.Instance.LoadPositionAndLevelFromFile(directoryName);


                Debug.Log("Data loaded successfully.");
            }
            else
            {
                Debug.LogWarning($"Full file path {fullFilePath} not found.");
            }
        }
    }

    public void DeleteSelectedSaveDirectory()
    {
        if (selectedLoadItem != null)
        {
            string fileName = selectedLoadItem.gameObject.GetComponentInChildren<TextMeshProUGUI>().text;
            string fullFilePath = Path.Combine(Application.dataPath, "Saves", fileName);
            Directory.Delete(fullFilePath, true);

            // Remove file from the list of saved files
            savedJSONFiles.Remove(fullFilePath);

            // Remove file from the scroll panel on the hand menu
            Destroy(selectedLoadItem);
            Debug.Log("File deleted.");
        }
        else { Debug.Log("No file selected."); }
    }

    private void InstantiateAndPopulateBubbles(ExtractedBubbleDataWrapper extractedBubbleDataWrapper, Dictionary<string, Bubble> bubblesToRelateToEachOther)
    {
        Debug.Log("Instantiation and Population");
        foreach (var data in extractedBubbleDataWrapper.extractedBubbleDatasList)
        {
            Bubble.BubbleType bubbleType = (Bubble.BubbleType)Enum.Parse(typeof(Bubble.BubbleType), data.elementType);
            GameObject bubbleObject = null;
            Bubble bubble = null;

            switch (bubbleType)
            {
                case Bubble.BubbleType.Class:
                    bubbleObject = LoadBubble(bubbleClassPrefab, data);
                    break;

                case Bubble.BubbleType.Property:
                    bubbleObject = LoadBubble(bubblePropertyPrefab, data);
                    break;

                case Bubble.BubbleType.Instance:
                    bubbleObject = LoadBubble(bubbleInstancePrefab, data);
                    break;
            }

            bubble = bubbleObject.GetComponent<Bubble>();

            if (bubbleObject != null && bubble != null)
            {
                bubblesToRelateToEachOther.Add(data.elementID, bubble);
                //AssignPropertiesValues(data, bubble, bubble.GetComponentInChildren<BubbleData>().bubbleDataSO);
            }
        }
    }

    private GameObject LoadBubble(GameObject prefab, ExtractedBubbleData data)
    {
        GameObject bubbleObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        bubbleObject.name = data.elementName;
        bubbleObject.transform.Find("Bubble Canvas Manager").GetComponentInChildren<TextMeshProUGUI>().text = data.elementName;

        var bubbleDataSO = bubbleObject.GetComponentInChildren<BubbleDataSO>();
        //Debug.Log($"OM Calling ServerRpc with for {bubbleDataSO} with {data.elementID}");

        //bubbleDataSO.elementID = data.elementID;
        //bubbleDataSO.elementName = data.elementName;
        //bubbleDataSO.elementType = data.elementType;
        //bubbleDataSO.ontologyTag = data.ontologyTag;
        //       SetBubbleDataSOClientRpc(bubbleObject, data);

        bubbleObject.GetComponent<NetworkObject>().Spawn();

        bubbleDataSO.ChangeElementID(data.elementID);
        bubbleDataSO.ChangeElementName(data.elementName);

        bubbleDataSO.ChangeElementType(data.elementType);
        bubbleDataSO.ChangeOntologyTag(data.ontologyTag);


        List<GameObject> ontologies = parentObject.GetComponent<Ontologies>().ontologies;
        GameObject foundOntology = ontologies.FirstOrDefault(obj => obj.name == data.ontologyTag);

        if (foundOntology != null)
        {
            bubbleObject.transform.SetParent(foundOntology.transform);
            foundOntology.GetComponent<Ontology>().classes.Add(bubbleObject);
        }
        else
        {
            // instantiating
            GameObject newOntology = Instantiate(ontologyPrefab);
            newOntology.name = data.ontologyTag;
            GameObject ontologyContainer = Instantiate(ontologyContainerPrefab);

            // adding to lists
            parentObject.GetComponent<ContainersManager>().ontologyContainers.Add(ontologyContainer);
            parentObject.GetComponent<Ontologies>().ontologies.Add(newOntology);
            newOntology.GetComponent<Ontology>().classes.Add(bubbleObject);

            // spawning network objects
            ontologyContainer.GetComponent<NetworkObject>().Spawn();
            newOntology.GetComponent<NetworkObject>().Spawn();

            // assigning parents
            ontologyContainer.transform.SetParent(newOntology.transform);
            newOntology.transform.parent = parentObject.transform;
            bubbleObject.transform.SetParent(newOntology.transform);
        }

        SearchManager.GetComponent<SearchScript>().Elements.Add(bubbleObject);

        //bubbleObject.GetComponentInChildren<BubbleButtonManager>().UIManager = UIManager;
        //bubbleObject.GetComponentInChildren<BubbleButtonManager>().SetUIManager(UIManager);

        return bubbleObject;
    }


    private void EstablishBubbleRelationships(ExtractedBubbleDataWrapper extractedBubbleDataWrapper, Dictionary<string, Bubble> bubblesToRelateToEachOther)
    {
        //Debug.Log("Establishing relationships");
        foreach (var data in extractedBubbleDataWrapper.extractedBubbleDatasList)
        {
            if (bubblesToRelateToEachOther.TryGetValue(data.elementID, out Bubble bubble))
            {
                BubbleDataSO dataSO = bubble.GetComponentInChildren<BubbleData>().bubbleDataSO;

                // Assign relationships
                AssignInheritedBubbles(data, dataSO, bubblesToRelateToEachOther, bubble);
                AssignHasBubbles(data, dataSO, bubblesToRelateToEachOther, bubble);
                AssignConnectionLabels(data, bubble);
                AssignPropertiesValues(data, bubble);
                bubble.RegisterWithRelatedBubbles();

                // Create connectors based on relationships
                bubble.InitializeAllConnectors();

                StartCoroutine(DelayNameTagChangeRoutine(data, bubble));
            }
        }
    }

    IEnumerator DelayNameTagChangeRoutine(ExtractedBubbleData data, Bubble bubble)
    {
        // Wait for 3 seconds
        yield return new WaitForSeconds(5f);

        // Now you can change the name tags
        for (int i = 0; i < bubble.ownedByConnectorsList.Count; i++)
        {
            TextMeshProUGUI nametagText = bubble.ownedByConnectorsList[i].currentNametag.GetComponentInChildren<TextMeshProUGUI>();
            string[] parts = data.ownedByBubblesIDsList[i].Split("/");
            string tag = parts[parts.Length - 1];

            nametagText.text = tag;
        }
    }

    private void AssignInheritedBubbles(ExtractedBubbleData data, BubbleDataSO dataSO, Dictionary<string, Bubble> bubblesToRelateToEachOther, Bubble bubble)
    {
        if (data.inheritedBubblesIDsList != null)
        {
            foreach (var inheritedBubbleID in data.inheritedBubblesIDsList)
            {
                if (bubblesToRelateToEachOther.TryGetValue(inheritedBubbleID, out Bubble inheritedBubble))
                {
                    dataSO.inheritedBubblesList.Add(inheritedBubble);
                    bubble.inheritedBubblesList.Add(inheritedBubble);
                }
            }
        }
    }

    private void AssignHasBubbles(ExtractedBubbleData data, BubbleDataSO dataSO, Dictionary<string, Bubble> bubblesToRelateToEachOther, Bubble bubble)
    {
        if (data.hasBubblesIDsList != null)
        {
            foreach (var hasBubbleID in data.hasBubblesIDsList)
            {
                if (bubblesToRelateToEachOther.TryGetValue(hasBubbleID, out Bubble hasBubble))
                {
                    dataSO.hasBubblesList.Add(hasBubble);
                    bubble.hasBubblesList.Add(hasBubble);
                }
            }
        }
    }

    private void AssignPropertiesValues(ExtractedBubbleData data, Bubble bubble)
    {
        if (data.elementPropertyValues.Count > 0)
        {
            bubble.propertyValues.Add(data.elementPropertyValues[0]);
            bubble.AssignPropertyValue(data.elementName, data.elementPropertyValues[0]);
        }
    }

    private void AssignConnectionLabels(ExtractedBubbleData data, Bubble bubble)
    {
        if (data.ownedByBubblesIDsList != null)
        {
            foreach (string connection in data.ownedByBubblesIDsList)
            {
                bubble.connectionLabels.Add(connection);
            }
        }
    }

    public static void RemoveAllBubblesFromScene()
    {
        List<Bubble> allBubbles = FindObjectsOfType<Bubble>().ToList();
        if (allBubbles != null && allBubbles.Count > 0)
        {
            foreach (Bubble bubble in allBubbles)
            {
                Destroy(bubble.gameObject);
            }
        }
    }

    public void ImportDefaultScene()
    {
        selectedLoadItem = defaultLoadItem;
        Debug.Log("file selected " + defaultLoadItem.gameObject.GetComponentInChildren<TextMeshProUGUI>().text);
        LoadAllOntologyDataFromFiles();
    }

    [Serializable]
    public class ExtractedBubbleData
    {
        public string elementID;
        public string elementName;
        public string elementType;
        public string ontologyTag;
        public List<string> inheritedBubblesIDsList = new();
        public List<string> inheritingBubblesIDsList = new();
        public List<string> hasBubblesIDsList = new();
        public List<string> ownedByBubblesIDsList = new();
        public List<string> elementPropertyValues = new();

        public ExtractedBubbleData(BubbleDataSO data)
        {
            this.elementID = data.GetElementID();
            this.elementName = data.GetElementName();
            this.elementType = data.GetElementType();
            this.ontologyTag = data.GetOntologyTag();

            //this.elementID = data.elementID;
            //this.elementName = data.elementName;
            //this.elementType = data.elementType;
            //this.ontologyTag = data.ontologyTag;

            data.inheritedBubblesList = data.GetInheritedBubblesList();

            if (data.inheritedBubblesList != null)
            {
                foreach (Bubble inheritedBubble in data.inheritedBubblesList)
                {
                    this.inheritedBubblesIDsList.Add(inheritedBubble.GetComponentInChildren<BubbleData>().bubbleDataSO.GetElementID());
                    //this.inheritedBubblesIDsList.Add(inheritedBubble.GetComponentInChildren<BubbleData>().bubbleDataSO.elementID);
                }
            }
            else
            {
                this.inheritedBubblesIDsList = null;
            }

            data.inheritingBubblesList = data.GetInheritingBubblesList();
            if (data.inheritingBubblesList != null)
            {
                foreach (Bubble bubble in data.inheritingBubblesList)
                {
                    inheritingBubblesIDsList.Add(bubble.GetComponentInChildren<BubbleData>().bubbleDataSO.GetElementID());
                    //inheritingBubblesIDsList.Add(bubble.GetComponentInChildren<BubbleData>().bubbleDataSO.elementID);
                }
            }
            else
            {
                inheritingBubblesIDsList = null;
            }

            data.hasBubblesList = data.GetHasBubblesList();
            if (data.hasBubblesList != null)
            {
                foreach (Bubble bubble in data.hasBubblesList)
                {
                    hasBubblesIDsList.Add(bubble.GetComponentInChildren<BubbleData>().bubbleDataSO.GetElementID());
                    //hasBubblesIDsList.Add(bubble.GetComponentInChildren<BubbleData>().bubbleDataSO.elementID);
                }
            }
            else
            {
                hasBubblesIDsList = null;
            }

            data.ownedByBubblesList = data.GetOwnedByBubblesList();
            data.connectionLabels = data.GetConnetionLabels();
            if (data.ownedByBubblesList != null)
            {
                for (int i = 0; i < data.ownedByBubblesList.Count; i++)
                {
                    Bubble bubble = data.ownedByBubblesList[i];
                    /* as we want to save the connection label, we look for the corresponding element in the list to save
                     and connectionLabels hold both the id and the label tag */
                    for (int j = 0; j < data.connectionLabels.Count; j++)
                    {
                        string bubbleID = bubble.GetComponentInChildren<BubbleData>().bubbleDataSO.GetElementID();
                        string[] labels = data.connectionLabels[j].Split("/");
                        if (labels[0] == bubbleID)
                        {
                            ownedByBubblesIDsList.Add(data.connectionLabels[j]);
                        }
                    }
                }
            }
            else
            {
                ownedByBubblesIDsList = null;
            }

            data.elementPropertyValues = data.GetElementPropertyValues();

            if (data.elementPropertyValues != null)
            {
                foreach (string comment in data.elementPropertyValues)
                {
                    elementPropertyValues.Add(comment);
                }
            }
            else
            {
                elementPropertyValues = null;
            }
        }
    }

    [Serializable]
    public class ExtractedBubbleDataWrapper
    {
        public List<ExtractedBubbleData> extractedBubbleDatasList = new();
    }
}
