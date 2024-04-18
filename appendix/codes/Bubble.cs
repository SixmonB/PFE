using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Netcode;
using System;
using System.Collections;
using TMPro;
/// <summary>
/// Represents a bubble object in the game.
/// </summary>
[System.Serializable]
public class Bubble : NetworkBehaviour, IEquatable<Bubble>
{
    /// Required for comparison of custom objects and their lists.
    public Guid UniqueID { get; private set; }

    public bool Equals(Bubble other)
    {
        if (other == null) return false;
        return UniqueID == other.UniqueID;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Bubble)obj);
    }
    public override int GetHashCode()
    {
        return UniqueID.GetHashCode();
    }

    public enum BubbleType
    {
        Class,
        Instance,
        Property
    }

    [HideInInspector]
    [Tooltip("The type of this bubble. (Class, Instance or Property)")]
    public BubbleType bubbleType;

    [Space(10)]
    [Header("Bubble Relations")]
    [Tooltip("The bubble from which this bubble inherits.")]
    public List<Bubble> inheritedBubblesList = new();

    [Tooltip("List of bubbles directly connected to this bubble.")]
    [SerializeField]
    private List<Bubble> directlyConnectedBubblesList = new();

    [Tooltip("List of bubbles that inherit from this bubble.")]
    public List<Bubble> inheritingBubblesList = new();

    [Tooltip("List of bubbles that this bubble connects to")]
    public List<Bubble> hasBubblesList = new();
    
    public NetworkList<ulong> hasBubblesIDList; 

    [Tooltip("List of labels for the connections to the bubbles that this bubble has.")]
    public List<string> connectionLabels = new();

    [Tooltip("List of comments/properties of each bubble.")]
    public List<string> propertyValues = new();

    [Tooltip("List of bubbles that connect to this bubble.")]
    public List<Bubble> ownedByBubblesList = new();

    [Space(10)]
    [Header("Connections and Related")]

    [Tooltip("List of connectors from bubbles owning this bubble.")]
    public List<Connector> ownedByConnectorsList = new();

    [Tooltip("List of connections involving this bubble.")]
    [SerializeField] private List<Connector> directGeneralConnections;

    [Space(10)]
    [Header("Bubble Components and Settings")]
    [Tooltip("List of all canvases that this bubble owns.")]
    List<Canvas> listOfAllCanvases = new();

    [Tooltip("The interactable component object of the bubble.")]
    VR_BubbleInteract interactableChild;

    [Tooltip("The teleport anchor component object of the bubble.")]
    VR_TeleportAnchorWithFade teleportableAnchorChild;

    [Tooltip("The scriptable object holding connector types.")]
    protected ConnectorTypesSO connectorTypesScriptableObject;

    [Tooltip("The current downward connector of the bubble.")]
    public Connector CurrentDownwardConnector { get; set; }

    //[HideInInspector]
    [Tooltip("List of connections to be recalculated.")]
    public List<Connector>
    connectionsToRecalculate = new();

    [Tooltip("The type of the connection that the bubble is involved in.")]
    protected int typeOfConnection = 0;

    [Space(10)]
    [Header("Children Bubbles")]
    [SerializeField] private GameObject _visualBubble;
    public GameObject VisualBubble { get { return _visualBubble; } }
    [SerializeField] private GameObject _vRBubbleTeleport;
    public GameObject VRBubbleTeleport { get { return _vRBubbleTeleport; } }
    [SerializeField] private GameObject _bubbleMenuManager;
    public GameObject BubbleMenuManager { get { return _bubbleMenuManager; } }
    [SerializeField] private GameObject _bubbleInfoCanvas;
    public GameObject BubbleInfoCanvas { get { return _bubbleInfoCanvas; } }
    [SerializeField] private TMP_Text propertyText;
    [SerializeField] private TMP_Text propertyName;

    protected virtual void Awake()
    {
        UniqueID = Guid.NewGuid();
        interactableChild = GetComponentInChildren<VR_BubbleInteract>();
        teleportableAnchorChild = GetComponentInChildren<VR_TeleportAnchorWithFade>();
        connectorTypesScriptableObject = Resources.Load<ConnectorTypesSO>("BubbleConnectorsSO");
        hasBubblesIDList = new NetworkList<ulong>();
    }
    public override void OnNetworkSpawn()
    {
        hasBubblesIDList.OnListChanged += OnhasBubblesIDListChanged;
    }

    public override void OnNetworkDespawn()
    {
        hasBubblesIDList.OnListChanged -= OnhasBubblesIDListChanged;
    }

    protected virtual void Start()
    {
        ManageXRScripts();
    }

    void ManageXRScripts()
    {
        if (RealityTypeDetector.Instance.CurrentRealityType == RealityTypeDetector.RealityType.AR)
        {
            // Here add all VR specific Bubble elements or components which should be deactivated if the application runs in the AR mode
            GameObject VRBubbleInteract = GameObject.Find("VR Bubble Interact");
            GameObject VRBubbleTeleport = GameObject.Find("VR Bubble Teleport");

            VRBubbleInteract.SetActive(false);
            VRBubbleTeleport.SetActive(false);
        }
        else
        {
            // Here add all AR specific Bubble elements or components which should be deactivated if the application runs in the VR mode
            GameObject ARBubbleInteract = GameObject.Find("AR Bubble Interact");

            ARBubbleInteract.SetActive(false);
        }
    }

    void OnhasBubblesIDListChanged(NetworkListEvent<ulong> changeEvent)
    {
        Debug.Log($"[S] The list changed and now has {hasBubblesIDList.Count} elements");
        ulong id = hasBubblesIDList[hasBubblesIDList.Count - 1];
        Debug.Log($"Id: {id}");
    }

    /// <summary>
    /// Registers this Bubble object with the related Bubbles to establish bidirectional relationships.
    /// This method serves to automate relationship management, reducing the need for manual drag-and-drop linking in the Unity Editor.
    /// </summary>
    public void RegisterWithRelatedBubbles()
    {
        if (inheritedBubblesList.Count != 0)
        {
            foreach (Bubble bubble in inheritedBubblesList)
            {
                bubble.inheritingBubblesList.Add(this);
            }
        }
        if (hasBubblesList.Count > 0)
        {
            foreach (Bubble bubble in hasBubblesList)
            {
                //bubble.ownedByBubblesList.Add(this);
                Debug.Log($"{NetworkObject.NetworkObjectId} added to {bubble} List");
                bubble.hasBubblesIDList.Add(NetworkObject.NetworkObjectId);
                bubble.AddToHasBubblesListClientRpc(bubble.NetworkObject, this.NetworkObject);
                Debug.Log($"{bubble} List: {bubble.hasBubblesIDList}");
            }

        }
    }

    [ClientRpc]
    public void AddToHasBubblesListClientRpc(NetworkObjectReference bubble, NetworkObjectReference bubbleToAdd)
    {
        Debug.Log($"ClientRPC - object: {bubble.NetworkObjectId} to Add: {bubbleToAdd}");
        if(bubble.TryGet(out NetworkObject bubbleObject))
        {
            if(bubbleToAdd.TryGet(out NetworkObject bubbleToAddObject))
            {
                Debug.Log($"ClientRPC - object: {bubbleObject} to Add: {bubbleToAddObject}");
                bubbleObject.GetComponent<Bubble>().ownedByBubblesList.Add(bubbleToAddObject.gameObject.GetComponent<Bubble>());
            }            
        }      
    }

    private Connector CreateConnection(Connector.ConnectorType connectorType, Bubble inheritedBubble, string connectorName)
    {
        var connectorPrefab = connectorTypesScriptableObject.connectors[(int)connectorType];
        var connector = Instantiate(connectorPrefab, transform);

        connector.name = $"{connectorName} connector";
        connector.connectorType = connectorType;
        connector.bubbleTaking = this;
        connector.bubbleGiving = inheritedBubble;

        return connector;
    }

    public void CreateDownwardConnection()
    {
        foreach (Bubble inheritedBubble in inheritedBubblesList)
        {
            var connectorType = GetConnectorType(inheritedBubble.bubbleType, bubbleType);
            CurrentDownwardConnector = CreateConnection(connectorType, inheritedBubble, connectorType.ToString());
            AddToDirectGeneralConnectionsList(CurrentDownwardConnector);
            inheritedBubble.AddToDirectGeneralConnectionsList(CurrentDownwardConnector);
        }

    }

    public void CreateHasPropertyConnections()
    {
        Connector.ConnectorType connectorType = Connector.ConnectorType.HasElementConnector;

        foreach (Bubble bubble in hasBubblesList)
        {
            Connector hasConnector = CreateConnection(connectorType, bubble, connectorType.ToString());

            this.AddToDirectGeneralConnectionsList(hasConnector);
            bubble.AddToDirectGeneralConnectionsList(hasConnector);

            bubble.AddConnectorFromOwningBubble(hasConnector);
        }
    }

    public void InitializeAllConnectors()
    {
        if (inheritedBubblesList.Count != 0) InitializeDownwardConnections();
        if (hasBubblesList.Count > 0) InitializeHasPropertyConnections();
    }

    public void InitializeDownwardConnections()
    {
        if (inheritedBubblesList.Count != 0) CreateDownwardConnection();
    }

    public void InitializeHasPropertyConnections()
    {
        if (hasBubblesList.Count > 0) CreateHasPropertyConnections();
    }

    // Defines the type of connector based on this bubble type and the inherited bubble type
    private Connector.ConnectorType GetConnectorType(BubbleType inheritedType, BubbleType currentType)
    {
        switch ((inheritedType, currentType))
        {
            case (BubbleType.Class, BubbleType.Class):
                return Connector.ConnectorType.ClassToClassConnector;
            case (BubbleType.Class, BubbleType.Instance):
                return Connector.ConnectorType.InstanceToClassConnector;
            case (BubbleType.Instance, BubbleType.Property):
                return Connector.ConnectorType.PropertyToInstanceConnector;
            case (BubbleType.Class, BubbleType.Property):
                return Connector.ConnectorType.PropertyToClassConnector;
            default:
                return Connector.ConnectorType.DefaultConnector;
        }
    }

    private void AddConnectionToLists(Connector connector)
    {

    }

    /// <summary>
    /// Adds a connected bubble to the bubble's list.
    /// </summary>
    /// <param name="bubble">The connected bubble to add.</param>
    public bool AddDirectlyConnectedBubbleToList(Bubble bubble)
    {
        directlyConnectedBubblesList ??= new List<Bubble>();
        if (directlyConnectedBubblesList != null && !directlyConnectedBubblesList.Contains(bubble))
        {
            directlyConnectedBubblesList.Add(bubble);
            return true;
        }
        else return false;
    }

    /// <summary>
    /// Adds a connection to the bubble's list.
    /// </summary>
    /// <param name="connection">The connection to add.</param>
    public void AddToDirectGeneralConnectionsList(Connector connection)
    {
        if (directGeneralConnections == null)
        {
            directGeneralConnections = new List<Connector>();
        }
        if (directGeneralConnections != null && !directGeneralConnections.Contains(connection))
        {
            directGeneralConnections.Add(connection);
        }
    }

    /// <summary>
    /// Adds a given Connector to the list that represents connections from owning bubbles.
    /// </summary>
    /// <param name="connection">The Connector to add.</param>
    /// <param name="owningBubble">The Bubble that owns this Connector. Not added to any list within the method.</param>
    public void AddConnectorFromOwningBubble(Connector connection)
    {
        this.ownedByConnectorsList.Add(connection);
    }


    /// <summary>
    /// Gets the list of direct connections.
    /// </summary>
    /// <returns>The list of connections.</returns>
    public List<Connector> GetAllDirectConnections()
    {
        return directGeneralConnections;
    }

    /// <summary>
    /// Gets the list of owned connectors.
    /// </summary>
    /// <returns>The list of owned connectors.</returns>
    public List<Connector> GetAllOwnedByConnections()
    {
        return ownedByConnectorsList;
    }

    /// <summary>
    /// Gets the list of connected bubbles.
    /// </summary>
    /// <returns>The list of connected bubbles.</returns>
    public List<Bubble> GetAllDirectlyConnectedBubbles()
    {
        directlyConnectedBubblesList.Clear();

        foreach (Bubble inheritedBubble in inheritedBubblesList)
        {
            directlyConnectedBubblesList.Add(inheritedBubble);
        }

        foreach (Bubble bubble in inheritingBubblesList)
        {
            AddDirectlyConnectedBubbleToList(bubble);
        }

        foreach (Bubble bubble in hasBubblesList)
        {
            AddDirectlyConnectedBubbleToList(bubble);
        }

        foreach (Bubble bubble in ownedByBubblesList)
        {
            AddDirectlyConnectedBubbleToList(bubble);
        }

        return directlyConnectedBubblesList;
    }


    /// <summary>
    /// Gets the list of connected bubbles upstream (including this bubble).
    /// </summary>
    public List<Bubble> GetAllBubblesUpstream()
    {
        List<Bubble> upstreamBubbles = new List<Bubble> { this };

        if (inheritingBubblesList.Count != 0)
        {
            foreach (Bubble bubble in inheritingBubblesList)
            {
                upstreamBubbles.AddRange(bubble.GetAllBubblesUpstream());
            }
        }

        return upstreamBubbles;
    }

    /// <summary>
    /// Gets the list of connected bubbles downstream (including this bubble).
    /// </summary>
    public List<Bubble> GetAllBubblesDownstream()
    {
        List<Bubble> downstreamBubbles = new() { this };

        if (inheritedBubblesList.Count != 0)
        {
            foreach (Bubble inheritedBubble in inheritedBubblesList)
            {
                downstreamBubbles.AddRange(inheritedBubble.GetAllBubblesDownstream());
            }
        }
        return downstreamBubbles;
    }

    /// <summary>
    /// Gets the list of connected bubbles being had (but not the bubble itself).
    /// </summary>
    public List<Bubble> GetAllBubblesOwned()
    {
        List<Bubble> bubblesBeingHad = new();

        if (hasBubblesList.Count != 0)
        {
            foreach (Bubble bubble in hasBubblesList)
            {
                bubblesBeingHad.Add(bubble);
            }
        }

        return bubblesBeingHad;
    }

    protected List<Bubble> GetAllBubblesOwningThisBubble()
    {
        return ownedByBubblesList;
    }

    /// <summary>
    /// Gets the list of connected bubbles downstream and upstream (including this bubble).
    /// </summary>
    public List<Bubble> GetAllBubblesInTree()
    {
        List<Bubble> bubblesInTree = new List<Bubble>();

        bubblesInTree.AddRange(GetAllBubblesUpstream());
        bubblesInTree.AddRange(GetAllBubblesDownstream());
        bubblesInTree.AddRange(GetAllBubblesOwned());

        bubblesInTree = bubblesInTree.Distinct().ToList();

        return bubblesInTree;
    }

    public void ShowBubble()
    {
        ToggleBubbleVisibility(true);
    }

    public void HideBubble()
    {
        ToggleBubbleVisibility(false);
    }

    public void ShowConnections()
    {
        ToggleConnectorVisibility(true);
    }
    public void HideConnections()
    {
        ToggleConnectorVisibility(false);
    }

    /// <summary>
    /// Hides or shows the bubble by deactivating or activating its various children's components.
    /// </summary>
    private void ToggleBubbleVisibility(bool isVisible)
    {
        // This following line will also get Renderers of connections created FROM the bubble being hidden
        transform.GetComponentsInChildren<Renderer>().ToList().ForEach(x => x.enabled = isVisible);
        interactableChild.enabled = isVisible;
        teleportableAnchorChild.enabled = isVisible;

        listOfAllCanvases = GetComponentsInChildren<Canvas>().ToList();

        listOfAllCanvases.ForEach(x => x.enabled = isVisible);
    }

    private void ToggleConnectorVisibility(bool isVisible)
    {
        if (directGeneralConnections != null)
        {
            foreach (Connector connector in directGeneralConnections)
            {
                if (connector == null)
                {
                    Debug.Log("Missing connection!");
                }
                else
                {
                    connector.ToggleConnectorOnOff(isVisible);
                    connector.currentNametag.GetComponentInChildren<Canvas>().enabled = isVisible;
                }

            }
        }
    }

    // Update the holder position only when the object is being interacted with
    public void UpdatePositionOfCompositeObjectsOfBubble()
    {
        if (interactableChild != null && interactableChild.isSelected)
        {
            this.transform.position = interactableChild.transform.position;
            this.transform.rotation = interactableChild.transform.rotation;
        }
        else if (interactableChild == null)
        {
            Debug.Log("Grab Interactable is null");
        }
    }

    public void RecalculateAllConnectorsFromList()
    {
        //if (connectionsToRecalculate != null)
        //{
        //    foreach (Connector connection in connectionsToRecalculate)
        //    {
        //        //Easy workaround for missing connection after deleting bubble.
        //        //Needs to be implemented in a proper way!
        //        if (connection == null)
        //        {
        //            Debug.Log("Missing connection!");
        //        }
        //        else
        //        {
        //            connection.RecalculateConnector();
        //        }
        //    }
        //}
        //Debug.Log($"Entered Recalculate for {NetworkManager.Singleton.LocalClientId}");
        RecalculateAllConnectorsFromListServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RecalculateAllConnectorsFromListServerRpc()
    {
        //Debug.Log($"Entered RecalculateServerRpc for {NetworkManager.Singleton.LocalClientId}");
        RecalculateAllConnectorsFromListClientRpc();
    }

    [ClientRpc]
    public void RecalculateAllConnectorsFromListClientRpc()
    {
        //Debug.Log($"Entered RecalculateClientRpc for {NetworkManager.Singleton.LocalClientId}");
        connectionsToRecalculate = GetAllDirectConnections();
        foreach (Connector connection in connectionsToRecalculate)
        {
            if (connectionsToRecalculate != null)
            {
                connection.RecalculateConnector();
            }
        }
    }

    // Used from within the editor
    public void DestroyThisBubble()
    {
        transform.parent.GetComponent<Ontology>().classes.Remove(gameObject);
        Destroy(gameObject);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        foreach (Connector connector in GetAllDirectConnections())
        {
            Destroy(connector.gameObject);
        }

        // Remove the meta data
        BubbleDataSO bubbleDataSOToRemove = GetComponentInChildren<BubbleData>().bubbleDataSO;
        Destroy(bubbleDataSOToRemove);

        // Remove bubbles from each other's lists
        List<Bubble> directlyConnectedBubbles = GetAllDirectlyConnectedBubbles();
        foreach (Bubble bubble in directlyConnectedBubbles)
        {
            if (bubble != null)
            {
                bubble.ownedByBubblesList.Remove(this);
                bubble.hasBubblesList.Remove(this);
                bubble.inheritingBubblesList.Remove(this);
                bubble.inheritedBubblesList.Remove(this);
            }
        }
    }

    public void AssignPropertyValue(string name, string text)
    {
        propertyName.text = name;
        propertyText.text = text;
    }
}

