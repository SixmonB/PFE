using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;


public class CommentObject
{
    [JsonProperty("@language")]
    public string Language = "en"; 
    [JsonProperty("@value")]
    public string Value { get; set; }
}

public class GenericObject
{
    [JsonProperty("@id")]
    public string ID { get; set; }
}

public class DomainRangeObject
{
    [JsonProperty("@id")]
    public string ID { get; set; }
}

public class RestrictionObject
{
    [JsonProperty("rdf:type")]
    public GenericObject type {get; set;}
    [JsonProperty("owl:onProperty")]
    public GenericObject property {get; set;}
    [JsonProperty("owl:someValuesFrom")]
    public GenericObject destination {get; set;}
}
public class ImportedBubbleInformation
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
}

public class ImportedPositionInformation
{
    public string bubbleID;
    // default values in case the imported class doesnt have position values
    public float posX = 0.0f;
    public float posY = 15.0f;
    public float posZ = 0.0f;
}

public class ImportJsonToOntology
{
    public List<ImportedBubbleInformation> extractedBubbleDatasList = new List<ImportedBubbleInformation>();
}

public class ImportJsonToOntologyPositions
{
    public List<ImportedPositionInformation> extractedPositionDataList = new List<ImportedPositionInformation>();
}
public class Parser : MonoBehaviour
{
    [SerializeField] private List<string> jsonLDFilePaths = new();
    [SerializeField] private List<string> jsonFilePaths = new();
    ImportJsonToOntology ontology = new ImportJsonToOntology();
    ImportJsonToOntologyPositions positions = new ImportJsonToOntologyPositions();

    private List<JObject> classObjects = new();
    private List<JObject> propertyObjects = new();
    private int firstID = 1000;
    private int IDcounter;
    private JArray exportToJson = new();
    private JObject ontologyContext;
    private string divider = "\n---\n"; // string divider for labels
    private string separator = "/"; // string separator for owner ID and label of the connection
    private JArray myGraph = new(); // this list will first hold a JObject and then dictionaries

    void Start()
    {
        ParseManager();
    }

    private void ParseManager()
    {
        ReadFiles();
        ReadMyOntology();
        ParseGaiax();
        ParseMyOntology();
        SaveJson(ontology,positions);
    }

    private void ParseGaiax()
    {
        FindSubclasses("http://www.w3.org/2000/01/rdf-schema#subClassOf");
        FindConnections("http://www.w3.org/2000/01/rdf-schema#domain", 
                        "http://www.w3.org/2000/01/rdf-schema#range",
                        "http://www.w3.org/2000/01/rdf-schema#label");
        FindComments("http://www.w3.org/2000/01/rdf-schema#comment");
    }

    private void ParseMyOntology()
    {
        FindSubclasses("rdfs:subClassOf");
    }
    private void ReadFiles()
    {
        IDcounter = firstID;
        HashSet<string> encounteredIDs = new HashSet<string>();
        for(int j = 0; j < jsonLDFilePaths.Count; j++)
        {
            string folder = "JSON";
            string fullPath = Path.Combine(Application.dataPath, folder, jsonLDFilePaths[j]);

            string rawJsonLdData = File.ReadAllText(fullPath);
            string jsonLdData = string.Copy(rawJsonLdData);

            fullPath = Path.Combine(Application.dataPath, folder, jsonFilePaths[j]);
            string rawJsonData = File.ReadAllText(fullPath);
            string jsonData = string.Copy(rawJsonData);

            JArray jsonLdArray = JsonConvert.DeserializeObject<JArray>(jsonLdData);

            JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonData);

            JArray classAttributesArray = jsonObject["classAttribute"] as JArray;

            foreach (JObject jsonLdObject in jsonLdArray)
            {
                JArray typeArray = jsonLdObject["@type"] as JArray;

                ImportedBubbleInformation newBubbleData = new ImportedBubbleInformation();
                ImportedPositionInformation newPositionData = new ImportedPositionInformation();

                string type = typeArray[0].Value<string>();
                string id = jsonLdObject["@id"].Value<string>();

                // we are looking for objects with only one type
                // so they can be a class or an object property connecting classes only
                // and also they have to belong to the specific gaia-x ontologies
                if (type.Contains("owl#Class") && id.Contains("http://w3id.org/gaia-x/"))
                {
                    if (!encounteredIDs.Contains(id)) // Check if the ID has not been encountered before.
                    {
                        encounteredIDs.Add(id);
                        string[] parts = id.Split('/'); // separates the url in the iri base url and the ontology+class
                        string result = parts[parts.Length - 1]; // result = ontology+class

                        parts = result.Split('#');
                        string tag  = parts[0]; // tag = ontology

                        newBubbleData.elementName = result;
                        // we'll only display the class name in the bubble but the class can be repeated in different ontologies, so we still need to specify which one it is
                        newBubbleData.elementType = "Class";
                        newBubbleData.elementID = IDcounter.ToString();
                        newBubbleData.ontologyTag = tag;
                        newPositionData.bubbleID = newBubbleData.elementID;
                        
                        float[] pos = FindPosByIRIValue(classAttributesArray, id);
                        newPositionData.posX = pos[0];
                        newPositionData.posY = pos[1];

                        classObjects.Add(jsonLdObject);
                        ontology.extractedBubbleDatasList.Add(newBubbleData);
                        positions.extractedPositionDataList.Add(newPositionData);
                        IDcounter++;
                    }
                }  

                else if (type.Contains("owl#ObjectProperty") && typeArray.Count == 1)
                {
                    propertyObjects.Add(jsonLdObject);
                }
                else
                {
                    exportToJson.Add(jsonLdObject);
                }      
            }
        }
    }

    private void FindSubclasses(string key)
    {
        for (int i = 0; i < classObjects.Count; i++)
        {
            
            JObject classObject = classObjects[i];
            if (classObject.ContainsKey(key))
            {
                JToken subClassToken = classObject[key];
                if (subClassToken is JArray)
                {
                    JArray subClassArray = subClassToken as JArray;
                    foreach (JObject subClassObject in subClassArray)
                    {
                        ProcessSubclass(subClassObject, i);
                    }
                }
                else if (subClassToken is JObject)
                {
                    JObject subClassObject = subClassToken as JObject;
                    ProcessSubclass(subClassObject, i);
                }
            }
        }
    }

    /// <summary>
    /// This method will verify if the parent class is from gaiax or droneprovider format and find the right ID
    /// as well as finding the restrictions for the drone provider connections
    /// </summary>
    /// <param name="subClassObject"> the parent class from whom we inherit </param>
    /// <param name="currentIndex"> index of our bubble </param>
    private void ProcessSubclass(JObject subClassObject, int currentIndex)
    {
        if (subClassObject.ContainsKey("@id"))
        {
            string subClassID = subClassObject["@id"].Value<string>();

            string convertedID = ConvertToGaiaxFormat(subClassID);
            
            int subClassIndex = FindIndexOfBubbleById(ontology.extractedBubbleDatasList, convertedID);

            if(subClassIndex >= 0)
            {
                ontology.extractedBubbleDatasList[currentIndex].inheritedBubblesIDsList.Add((subClassIndex+firstID).ToString());
                ontology.extractedBubbleDatasList[subClassIndex].inheritingBubblesIDsList.Add((currentIndex+firstID).ToString());
            }
        }
        else if (subClassObject.ContainsKey("rdf:type")) // DroneProvider ontology specifies relationships between classes inside the "subClassOf" key 
        {
            JObject type = subClassObject["rdf:type"] as JObject;
            string id = type["@id"].Value<string>();
            if(id == "owl:Restriction")
            {
                if(subClassObject.ContainsKey("owl:someValuesFrom"))
                {
                    JObject ownerClass = subClassObject["owl:someValuesFrom"] as JObject;
                    string ownerClassName = ownerClass["@id"].Value<string>();
                    string convertedID = ConvertToGaiaxFormat(ownerClassName);

                    int ownerIndex = FindIndexOfBubbleById(ontology.extractedBubbleDatasList, convertedID);
                    if(ownerIndex >= 0)
                    {
                        JObject propertyName = subClassObject["owl:onProperty"] as JObject;
                        string label = propertyName["@id"].Value<string>();
                        string labelName = label;

                        VerifyListsForRepetitionAndAdd(ownerIndex, currentIndex, labelName);
                    }
                }


                // To contemplate the idea of importing "allValuesFrom" means that we should be able to export to it as well
                // To do that, we need to import and store which case it is we are dealing with


                // if(subClassObject.ContainsKey("owl:allValuesFrom"))
                // {
                //     ownerClass = subClassObject["owl:allValuesFrom"] as JObject;
                // }
                
            }
        }
    }

    private string ConvertToGaiaxFormat(string id)
    {
        // Check if the ID is in the "gax-" format
        if (id.StartsWith("gax-"))
        {
            // Remove "gax-" part, replace ":" with "#"
            string convertedID = id.Replace("gax-", "").Replace(":", "#");

            return convertedID;
        }
        else
        {
            string convertedID = id.Replace("0:", "droneprovider#");
            return convertedID;
        }

        // If the ID is not in the expected format, return it as is
        
    }

    private void FindConnections(string domain, string range, string label)
    {
        for (int i = 0; i < propertyObjects.Count; i++)
        {
            JObject propertyObject = propertyObjects[i];
            if (propertyObject.ContainsKey(domain) && propertyObject.ContainsKey(range)) 
            {
                string domainID;
                string rangeID;
                JArray domainArray = propertyObject[domain] as JArray;
                JArray rangeArray = propertyObject[range] as JArray;
                
                JObject domainObject = domainArray[0] as JObject;
                JObject rangeObject = rangeArray[0] as JObject;
                
                domainID = domainObject["@id"].Value<string>();
                rangeID = rangeObject["@id"].Value<string>();
                
                int domainIndex = FindIndexOfBubbleById(ontology.extractedBubbleDatasList, domainID);
                int rangeIndex = FindIndexOfBubbleById(ontology.extractedBubbleDatasList, rangeID);

                string connectionID = propertyObject["@id"].Value<string>();
                string[] parts = connectionID.Split("#");
                string labelName = parts[parts.Length - 1];

                VerifyListsForRepetitionAndAdd(domainIndex, rangeIndex, labelName);
                
            }
        }
    }

    /// <summary>
    /// we check if there is already an existing connection going from bubble1 to bubble2
    /// if there is one from bubble2 to bubble1, it wont count as an existing connection because the direction is different
    /// </summary>
    /// <param name="domainIndex"></param>
    /// <param name="rangeIndex"></param>
    /// <param name="labelName"></param>

    private void VerifyListsForRepetitionAndAdd(int domainIndex, int rangeIndex, string labelName)
    {
        if(domainIndex >= 0 && rangeIndex >= 0)
        {
            int indexInDomainList = ontology.extractedBubbleDatasList[domainIndex].hasBubblesIDsList.IndexOf((rangeIndex+firstID).ToString());
            if(indexInDomainList >= 0) // verification if there is already a connection between these 2 bubbles to just add the tag for the connection we are creating
            {   
                for(int i = 0; i < ontology.extractedBubbleDatasList[rangeIndex].ownedByBubblesIDsList.Count; i++)
                {
                    if(ontology.extractedBubbleDatasList[rangeIndex].ownedByBubblesIDsList[i].Contains((domainIndex+firstID).ToString())) // the list of owning bubbles also holds the label of the connection, so we verify if the list contains the id
                    {
                        int indexInRangeList = i;
                        ontology.extractedBubbleDatasList[rangeIndex].ownedByBubblesIDsList[indexInRangeList] += (divider + labelName); // if the connection already exists, we just add a divider and the new label
                        break;
                    }
                }                
                
            }
            else
            {
                ontology.extractedBubbleDatasList[rangeIndex].ownedByBubblesIDsList.Add((domainIndex+firstID).ToString() + separator + labelName);
                ontology.extractedBubbleDatasList[domainIndex].hasBubblesIDsList.Add((rangeIndex+firstID).ToString());
            }
        }
    }

    private int FindIndexOfBubbleById(List<ImportedBubbleInformation> bubbleList, string bubbleId)
    {
        string[] parts = bubbleId.Split('/');
        string result = parts[parts.Length - 1];

        for (int i = 0; i < bubbleList.Count; i++)
        {
            if (bubbleList[i].elementName == result)
            {
                return i; // Return the index of the bubble with the matching ID.
            }
        }
        return -1; // Return -1 if the bubble is not found.
    }

    private float[] FindPosByIRIValue(JArray classAttributesArray, string searchValue)
    {
        foreach (JObject classAttributeObject in classAttributesArray)
        {
            if(classAttributeObject.ContainsKey("iri"))
            {
                string iriObject = classAttributeObject["iri"].Value<string>();
                if (iriObject != null && iriObject == searchValue)
                {
                    JArray posArray = classAttributeObject["pos"] as JArray;

                    if (posArray != null)
                    {
                        // scaled values
                        float posX = posArray[0].Value<float>() / 100.0f;
                        float posY = posArray[1].Value<float>() / 100.0f;
                        float[] position = new float[] { posX, posY};
                        return position;
                    }
                }
            }
        }

        return new float[] { 0.0f, 15.0f }; // default return value. It's the same as the initialized values
    }

    private void SaveJson(ImportJsonToOntology ontology, ImportJsonToOntologyPositions positions)
    {
        string outputPath = "Saves/TestingJsonLD";
        string ontologyFile = "savedOntology_TestingJsonLD.json";
        string positionsFile = "savedPositions_TestingJsonLD.json";
        string fullPath = Path.Combine(Application.dataPath, outputPath, ontologyFile);

        string jsonData = JsonConvert.SerializeObject(ontology, Formatting.Indented);
        File.WriteAllText(fullPath, jsonData);

        fullPath = Path.Combine(Application.dataPath, outputPath, positionsFile);
        jsonData = JsonConvert.SerializeObject(positions, Formatting.Indented);

        File.WriteAllText(fullPath, jsonData);
    }
    private void ReadMyOntology()
    {
        string folder = "JSON";
        string fileName = "Droneprovider_v001.jsonld";
        string fullPath = Path.Combine(Application.dataPath, folder, fileName);

        string jsonLdData = File.ReadAllText(fullPath);

        fileName = "foaf.json";
        fullPath = Path.Combine(Application.dataPath, folder, fileName);

        string jsonData = File.ReadAllText(fullPath);

        JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonData);

        JArray classAttributesArray = jsonObject["classAttribute"] as JArray;

        JObject jsonLdObject = JObject.Parse(jsonLdData);

        ontologyContext = jsonLdObject["@context"] as JObject;
        JArray graph = jsonLdObject["@graph"] as JArray;
        if (graph != null)
        {
            foreach (JObject element in graph)
            {
                ImportedBubbleInformation newBubbleData = new ImportedBubbleInformation();
                ImportedPositionInformation newPositionData = new ImportedPositionInformation();

                if (element.ContainsKey("rdf:type") && element["rdf:type"] is JObject)
                {
                    JObject type = element["rdf:type"] as JObject;
                    string myType = type["@id"].Value<string>();
                    if(myType == "owl:Class")
                    {
                        newBubbleData.elementID = IDcounter.ToString();
                        newPositionData.bubbleID = newBubbleData.elementID;

                        string name = element["@id"].Value<string>();
                        string searchName = name.Replace("0:", "http://www.ipk.semantics.org/ontologies/konierik/droneprovider#");

                        
                        newBubbleData.elementType = "Class";
                        newBubbleData.ontologyTag = "droneprovider";
                        newBubbleData.elementName = name.Replace("0:", "droneprovider#");

                        float[] pos = FindPosByIRIValue(classAttributesArray, searchName);
                        newPositionData.posX = pos[0];
                        newPositionData.posY = pos[1];

                        IDcounter++;
                        classObjects.Add(element);
                        ontology.extractedBubbleDatasList.Add(newBubbleData);
                        positions.extractedPositionDataList.Add(newPositionData);
                    }
                    else if(myType == "owl:Ontology")
                    {
                        myGraph.Add(element);
                    }
                }
            }
        }
    }

    private void FindComments(string key)
    {
        for (int i = 0; i < classObjects.Count; i++)
        {  
            JObject classObject = classObjects[i];

            // string classObjectString = JsonConvert.SerializeObject(classObject, Formatting.Indented);
            // ontology.extractedBubbleDatasList[i].elementPropertyValues.Add(classObjectString);
        
            if (classObject.ContainsKey(key))
            {
                JArray commentArray = classObject[key] as JArray;
                foreach (JObject commentObject in commentArray)
                {
                    string comment = commentObject["@value"].Value<string>();
                    ontology.extractedBubbleDatasList[i].elementPropertyValues.Add(comment);
                }
            }

        }
    }

    public void ParseToJsonLD(string filePath, string folderPath, string saveFileName)
    {
        string jsonData = File.ReadAllText(filePath);

        JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonData);

        JArray bubblesDataList = jsonObject["extractedBubbleDatasList"] as JArray;

        foreach (JObject bubbleData in bubblesDataList)
        {
            Dictionary <string,object> dataToAdd = new();

            JArray elementPropertyValuesArray = bubbleData["elementPropertyValues"] as JArray;
            JArray subClassArray = bubbleData["inheritedBubblesIDsList"] as JArray;
            JArray ownedByBubblesArray = bubbleData["ownedByBubblesIDsList"] as JArray; 

            if(bubbleData["ontologyTag"].Value<string>() == "droneprovider")
            {
                dataToAdd["@id"] = bubbleData["elementName"].Value<string>( );

                GenericObject type = new();
                type.ID = "owl:Class";

                dataToAdd["rdf:type"] = type;

                int amountOfSubclasses = subClassArray.Count + ownedByBubblesArray.Count;

                // in Erik's ontology, if there is more than one connection, either subclass or property, it will be added to a list. If there is only one connection, it will be a simple JObject
                
                List<object> connections = new();
                if(subClassArray.Count > 0)
                {      
                    List<string> names = GetNamesFromIDs(subClassArray, bubblesDataList, false);
                    if(amountOfSubclasses > 1)
                    {
                        connections.AddRange(names.Select(name => new GenericObject { ID = name }));
                    }
                    else
                    {
                        GenericObject connection = new GenericObject { ID = names[0] };
                        dataToAdd["rdfs:subClassOf"] = connection;
                    }
                }

                if(ownedByBubblesArray.Count>0)
                {
                    List<string>ownerNames = GetNamesFromIDs(ownedByBubblesArray, bubblesDataList, false);
                    for(int i = 0; i < ownedByBubblesArray.Count; i++)
                    {
                        string[] labels = ownedByBubblesArray[i].Value<string>().Split(separator);
                        string[] parts = labels[labels.Length-1].Split(divider);
                        foreach(string separateLabel in parts)
                        {
                            string ownerTag = GetTagFromID(ownedByBubblesArray[i].Value<string>(), bubblesDataList);
                            RestrictionObject connection = new RestrictionObject { 
                            type = new GenericObject { ID = "owl:Restriction" },
                            property = new GenericObject { ID = separateLabel },
                            destination = new GenericObject { ID = ownerNames[i] } 
                            };
                            if(amountOfSubclasses > 1 || parts.Length > 1) // there is more than one connection
                            {
                                connections.Add(connection); 
                                dataToAdd["rdfs:subClassOf"] = connections;
                            }
                            else
                            {
                                dataToAdd["rdfs:subClassOf"] = connection;
                            }
                        }
                    }
                }

                JObject jsonDataToAdd = JObject.FromObject(dataToAdd);
                myGraph.Add(jsonDataToAdd);

            }        
    
            else
            {
                Dictionary <string,object> dataToExport = new();
                dataToExport["@id"] = "http://w3id.org/gaia-x/" + bubbleData["elementName"].Value<string>();

                List<string> classTypeList = new();
                classTypeList.Add("http://www.w3.org/2002/07/owl#Class");
                dataToExport["@type"] = classTypeList;
                
                if(elementPropertyValuesArray.Count > 0)
                {
                    List<CommentObject> commentObjects = new();         
                    commentObjects.AddRange(elementPropertyValuesArray.Select(comment => new CommentObject { Value = comment.ToString() }));
                    
                    dataToExport["http://www.w3.org/2000/01/rdf-schema#comment"] = commentObjects;
                }

                if(subClassArray.Count > 0)
                {
                    List<GenericObject> subClassObjects = new();         
                    List<string> names = GetNamesFromIDs(subClassArray, bubblesDataList, true);
                    subClassObjects.AddRange(names.Select(name => new GenericObject { ID = name }));
                    dataToExport["http://www.w3.org/2000/01/rdf-schema#subClassOf"] = subClassObjects;
                }
                
                if(ownedByBubblesArray.Count > 0)
                {
                    List<string> ownerNames = GetNamesFromIDs(ownedByBubblesArray, bubblesDataList, true);
                    for(int i = 0; i < ownedByBubblesArray.Count; i++)
                    {
                        // labels are after the separator so we need to split the string to get them
                        string[] labels = ownedByBubblesArray[i].Value<string>().Split(separator);
                        string[] parts = labels[labels.Length-1].Split(divider);
                        foreach(string separateLabel in parts)
                        {
                            Dictionary<string,object> objectProperty = new();
                            string ownerTag = GetTagFromID(ownedByBubblesArray[i].Value<string>(), bubblesDataList);
                            objectProperty["@id"] = "http://w3id.org/gaia-x/" + ownerTag + "#" + separateLabel;
                            List<string> objectPropertyTypeList = new();
                            objectPropertyTypeList.Add("http://www.w3.org/2002/07/owl#ObjectProperty");
                            objectProperty["@type"] = objectPropertyTypeList;

                            List<DomainRangeObject> domainObjectList = new();
                            DomainRangeObject domainObject = new();
                            domainObject.ID = ownerNames[i];
                            domainObjectList.Add(domainObject);
                            objectProperty["http://www.w3.org/2000/01/rdf-schema#domain"] = domainObjectList;

                            List<DomainRangeObject> rangeObjectList = new();
                            DomainRangeObject rangeObject = new();
                            rangeObject.ID = "http://w3id.org/gaia-x/" + bubbleData["elementName"];
                            rangeObjectList.Add(rangeObject);
                            objectProperty["http://www.w3.org/2000/01/rdf-schema#range"] = rangeObjectList;
                            JObject newConnectionToExport = JObject.FromObject(objectProperty);
                            exportToJson.Add(newConnectionToExport);
                        }
                    }
                }
                JObject newBubbleToExport = JObject.FromObject(dataToExport);
                exportToJson.Add(newBubbleToExport);
            }            
        }

        JObject wrapperObject = new JObject();
        wrapperObject.Add("@context", ontologyContext);
        wrapperObject.Add("@graph", myGraph);
        exportToJson.Add(wrapperObject);

        string fullPath = Path.Combine(folderPath, saveFileName);

        string jsonDataToExport = JsonConvert.SerializeObject(exportToJson, Formatting.Indented);
        File.WriteAllText(fullPath, jsonDataToExport);
    }

    private List<string> GetNamesFromIDs(JArray IdList, JArray bubblesList, bool flag) // true = url ids, false = parsed ids 
    {
        List<string> names = new();
        if(flag)
        {
            for(int i = 0; i < IdList.Count; i++)
            {
                string idAndLabel = IdList[i].Value<string>();
                string[] parts = idAndLabel.Split(separator);
                string LookingForID = parts[0];
                foreach(JObject bubbleData in bubblesList)
                {
                    string bubbleID = bubbleData["elementID"].Value<string>();
                    if(LookingForID == bubbleID)
                    {
                        names.Add("http://w3id.org/gaia-x/" + bubbleData["elementName"].Value<string>());
                        break;
                    }
                }
            }
        }
        else // this routine is for when we are looking for classes names to add to the drone provider ontology in its own format.
        // for example a core#consumable class would be "gax-core:consumable", so we get the data and parse it
        {
            for(int i = 0; i < IdList.Count; i++)
            {
                string idAndLabel = IdList[i].Value<string>();
                string[] parts = idAndLabel.Split(separator);
                string LookingForID = parts[0];
                foreach(JObject bubbleData in bubblesList)
                {
                    string bubbleID = bubbleData["elementID"].Value<string>();
                    if(LookingForID == bubbleID)
                    {
                        if(bubbleData["ontologyTag"].Value<string>() != "droneprovider")
                        {
                            Array.Clear(parts, 0, parts.Length); // empty array
                            parts = bubbleData["elementName"].Value<string>().Split('#');
                            string result = parts[parts.Length-1];

                            names.Add("gax-" + bubbleData["ontologyTag"].Value<string>() + ":" + result);
                        }
                        else
                        {
                            names.Add(bubbleData["elementName"].Value<string>());
                        }
                        break;
                    }
                }
            }
        }
        
        
        return names;
    }

    private string GetTagFromID(string idAndLabel, JArray bubblesList)
    {
        // the id from the ownedByBubblesIDsList has also a separator and the connection label
        string[] parts = idAndLabel.Split(separator);
        string LookingForID = parts[0];
        string tag = "";
        foreach(JObject bubbleData in bubblesList)
        {
            string bubbleID = bubbleData["elementID"].Value<string>();
            if(LookingForID == bubbleID)
            {
                tag = bubbleData["ontologyTag"].Value<string>();
                break;
            }
        }
        return tag;
    }
}

