using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* CURRENT STATUS
 
 This feature works but has some bugs and miscalculations in the sphere shape.
I will include proper documentation explaining what isgoing on and how to fix it.

All the geometrical calculations for the sphere shape are made with the hypothesis of starting from
a flat layout, so either we switch to "flat" every time we switch layouts, or we generalize the calculation of nodes
for both shapes.
 */

public class Ontology : MonoBehaviour
{
    /// <summary>
    /// each ontology empty game object holds a list of all the classes it owns
    /// </summary>
    public List<GameObject> classes = new();
    private float zDefaultPosition = 0.0f; // verify in Parser.cs the default value in the ImportedPositionInformation class

    private void SortClasses(string axis)
    {
        switch(axis)
        {
            case "x":
                classes.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                break;
            case "y":
                classes.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
                break;
            case "z":
                classes.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
                break;
            default:
                break;
        }
        
    }

    private float FindRadius(Vector3 center)
    {
        float maxDistance = 0.0f;
        foreach(GameObject classObject in classes)
        {
            float distance = Vector3.Distance(classObject.transform.position, center);
            if(distance > maxDistance)
            {
                maxDistance = distance;
            }
        }
        return maxDistance;
    }


    public void SelectBubblesLayout(string mode)
    {
        switch (mode)
        {
            case "cylinder":

                /* radius will be calculed with the list in crecent order according to x position,
                so we need to sort first by z and then do x. Plus the first and last element wont be moved and will serve as
                opposites in the side of the cylinder
                 */
                SortClasses("z");
                float zClosestValue = classes[0].transform.position.z;
                SortClasses("x");
                float radius = (classes[classes.Count - 1].transform.position.x - classes[0].transform.position.x) / 2.0f;
                float cylinderCenter = classes[0].transform.position.x + radius;
                if (radius > 0) // this means that the ontology has more than 1 element
                {
                    for (int i = 0; i < classes.Count; i++)
                    {
                        float acosArgument = (classes[i].transform.position.x - cylinderCenter) / radius;
                        if(Mathf.Abs(acosArgument) > 1)
                        {
                            Debug.LogWarning("cosine out of order with a value of " + acosArgument);
                            if (acosArgument > 1) acosArgument = 1;
                            else if (acosArgument < -1) acosArgument = -1;
                        }
                        float angle = Mathf.Acos( acosArgument );
                        float zNewPosition = zClosestValue + Mathf.Sin(angle) * radius;
                        Vector3 newPosition = new Vector3(classes[i].transform.position.x, classes[i].transform.position.y, zNewPosition);
                        classes[i].GetComponent<Multiplayer_MoveObjects>().Multiplayer_VR_MoveBubble(newPosition);
                    }
                }
                break;

            /* the sphere layout will look at the classes of the ontology and look for the (x,y,z) coordinates
             * of the center by dividing by 2 the difference between the first and last element of the classes list
             * ( ordered according to each axis ). With this center, we'll look for the furthest bubble of the 
             * ontology and use that distance as the sphere radius. With that sphere equation and the (x,y)
             * coordinates of all bubbles, we'll look for the third one.
             * Note : there are 2 possibilites for each pair of x y values because of the sqrt. Take into consideration */
            case "sphere":


                /* the sphere doesnt matter so much which axis we use to sort the classes, as all of them will be calculated */
                SortClasses("z");
                zClosestValue = classes[0].transform.position.z;
                SortClasses("x");
                float xRadius = (classes[classes.Count - 1].transform.position.x - classes[0].transform.position.x) / 2.0f;
                float centerX = classes[0].transform.position.x + xRadius; 
                SortClasses("y");
                float yRadius = (classes[classes.Count - 1].transform.position.y - classes[0].transform.position.y) / 2.0f;
                float centerY = classes[0].transform.position.y + yRadius;

                Vector3 sphereCenter = new Vector3(centerX, centerY, zClosestValue);

                radius = FindRadius(sphereCenter);

                if (radius > 0)
                {
                    for(int i = 0; i < classes.Count; i++)
                    {
                        float xDistance = classes[i].transform.position.x - centerX;
                        float yDistance = classes[i].transform.position.y - centerY;

                        float zSquared = Mathf.Pow(radius, 2.0f) - Mathf.Pow(xDistance, 2.0f) - Mathf.Pow(yDistance, 2.0f);

                        if (zSquared >= 0) // Check if zSquared is non-negative
                        {
                            float zNewPosition = sphereCenter.z + Mathf.Sqrt(zSquared);

                            Vector3 newPosition = new Vector3(classes[i].transform.position.x, classes[i].transform.position.y, zNewPosition);
                            classes[i].GetComponent<Multiplayer_MoveObjects>().Multiplayer_VR_MoveBubble(newPosition);
                        }
                        else
                        {
                            //Debug.LogWarning("zSquared is negative for object: " + classes[i].name);
                            //Debug.LogWarning("xDistance: " + xDistance + ", yDistance: " + yDistance);
                            //Debug.LogWarning("Radius: " + radius);
                            //Debug.LogWarning("Zsquared = " + zSquared);

                            // after analysis and debug, I found that errors in calculation are because of floating point and approximation, so the
                            // square root returns a value very very close to 0 but negative, so we cant assign it
                            
                            Vector3 newPosition = new Vector3(classes[i].transform.position.x, classes[i].transform.position.y, 0.0f);
                            classes[i].GetComponent<Multiplayer_MoveObjects>().Multiplayer_VR_MoveBubble(newPosition);
                        }
                    }  
                }
                break;

            case "flat": // initial flat layout
                SortClasses("z");
                zClosestValue = classes[0].transform.position.z;
                foreach (GameObject obj in classes)
                {
                    Vector3 newPosition = new Vector3(obj.transform.position.x, obj.transform.position.y, zClosestValue);
                    obj.GetComponent<Multiplayer_MoveObjects>().Multiplayer_VR_MoveBubble(newPosition);
                }
                break;

            default:
                break;
        }
    }
}
