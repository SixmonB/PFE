using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using TMPro;

public class OntologyContainer : MonoBehaviour
{
    private float maxX, maxY, maxZ;
    private float minX, minY, minZ;
    private bool isInitialCase = true;
    private List<GameObject> classes = new();
    private MeshRenderer cubeRenderer = new();
    [SerializeField] private GameObject ContainerInteract;
    [SerializeField] private TMP_Text nameText;
    private void Start()
    {
        classes = gameObject.transform.parent.GetComponent<Ontology>().classes;
        ContainerInteract.GetComponent<VR_ContainerInteract>().classes = classes;
        cubeRenderer = gameObject.GetComponent<MeshRenderer>();
        nameText.text = transform.parent.name;
    }

    public void SpawnContainer()
    {
        isInitialCase = true;
        DefineSize();
        ContainerInteract.SetActive(true);
        nameText.gameObject.SetActive(true);
    }

    public void HideContainer()
    {
        cubeRenderer.enabled = false;
        ContainerInteract.SetActive(false);
        nameText.gameObject.SetActive(false);
    }

    private void DefineSize()
    {
        for (int i = 0; i < classes.Count; i++)
        {
            FindExtremes(classes[i].transform.position);
        }

        SetCubeSizeAndPosition();
    }

    private void SetCubeSizeAndPosition()
    {
        // Calculate size
        float sizeX = maxX - minX + 2.0f;
        float sizeY = maxY - minY + 2.0f;
        float sizeZ = maxZ - minZ + 2.0f;

        // Calculate center position
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;
        float centerZ = (minZ + maxZ) / 2f;

        // Set the cube's size and position
        gameObject.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);
        Vector3 newPosition = new Vector3(centerX, centerY, centerZ);
        gameObject.GetComponent<Multiplayer_MoveObjects>().Mutliplayer_UpdatePosition(newPosition); // spawn the container

        cubeRenderer.enabled = true;

        cubeRenderer.material.color = new Color(Random.value, Random.value, Random.value, 0.4f);
    }

    private void FindExtremes(Vector3 bubblePosition)
    {
        if(bubblePosition.x < minX || isInitialCase)
        {
            minX = bubblePosition.x;
        }
        if (bubblePosition.y < minY || isInitialCase)
        {
            minY = bubblePosition.y;
        }
        if (bubblePosition.z < minZ || isInitialCase)
        {
            minZ = bubblePosition.z;
        }

        if (bubblePosition.x > maxX || isInitialCase)
        {
            maxX = bubblePosition.x;
        }
        if (bubblePosition.y > maxY || isInitialCase)
        {
            maxY = bubblePosition.y;
        }
        if (bubblePosition.z > maxZ || isInitialCase)
        {
            maxZ = bubblePosition.z;
        }

        isInitialCase = false;
    }
}
