using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// updated script that does binary-search traversal
public class PickUp_binary : MonoBehaviour
{
    // Vars for object snapping to center (required for illusion to work)
    public float clampFactor = .01f;
    public float moveForce = 150f;

    // Vars to maintain distances
    private float distanceToHeld;
    private float scaleFactor;

    // Vars for handling held object
    public Transform holdParent;
    private GameObject heldObj;

    // Vars for collision checker
    public bool realtimeUpdate = true;
    public bool updateObjectPos = true;
    private float maxRayDist = 100f;
    public bool collidersVisible = false;
    public Material colliderMaterial;
    private GameObject collObj;

    // Update is called once per frame
    void Update()
    {
        // "E" input (pick-up / drop)
        if (Input.GetKeyDown(KeyCode.E))
        {
            // If nothing held,raycast to search for object
            if (heldObj == null)
            {
                RaycastHit hit;
                if (Physics.Raycast(transform.position,                             // ro
                                    transform.TransformDirection(Vector3.forward),  // rd
                                    out hit))                                       // raycast out
                {   
                    PickupObject(hit.transform.gameObject);
                }
            }
            // If something held, drop it
            else
            {
                DropObject();
            }
        }

        // Stuff to do if an object is being held
        if (heldObj != null)
        {
            // Snap object to center
            MoveObject();

            // Move holdParent along vector input
            if (Input.mouseScrollDelta.y != 0)
            {
                MoveHoldParent(Input.mouseScrollDelta.y);
            }
            
            // prediction checking
            if (realtimeUpdate || Input.GetKeyDown(KeyCode.Q))
            {
                collObj.transform.position = transform.position;
                RaycastToCollision();
            }
            

            // move/scale actual object
            if (updateObjectPos)
            {
                holdParent.transform.position = collObj.transform.position;
                ScaleObject(heldObj);
            }
        }
    }

    // Adjust physics properties, and attach object to holdParent
    void PickupObject(GameObject pickObj)
    {
        if (pickObj.GetComponent<Rigidbody>())
        {
            // OBJECT PARENTING
            // ----------------

            // Get and configure Object's Rigidbody
            Rigidbody objRig = pickObj.GetComponent<Rigidbody>();
            objRig.useGravity = false;
            objRig.freezeRotation = true;
            objRig.drag = 10;   // optional
            // objRig.isKinematic = true; // not sure if needed

            // Get distance from Player to Object
            float distanceToObj = Vector3.Distance(transform.position, objRig.transform.position)-2.0f;

            // Move target position (holdParent) to curr. distance along view vector
            holdParent.transform.position += transform.TransformDirection(Vector3.forward) * distanceToObj;

            // Update distance and scaleFactor
            distanceToHeld = Vector3.Distance(holdParent.transform.position, transform.position);
            scaleFactor = pickObj.transform.localScale.x / distanceToHeld;

            // Set object to current held object
            objRig.transform.parent = holdParent;
            heldObj = pickObj;
            heldObj.layer = LayerMask.NameToLayer("Ignore Raycast");
            heldObj.GetComponent<MeshCollider>().enabled = false;

            // COLLIDER STUFF
            // --------------

            // instantiate collider object
            collObj = Instantiate(heldObj, holdParent);
            // collObj = Instantiate(heldObj, transform);
            collObj.GetComponent<MeshCollider>().enabled = false;
            collObj.GetComponent<MeshRenderer>().material = colliderMaterial;
            collObj.GetComponent<MeshRenderer>().enabled = collidersVisible;
            collObj.layer = LayerMask.NameToLayer("Ignore Raycast");
            collObj.GetComponent<MeshCollider>().isTrigger = true; // ignore physics

            collObj.transform.position = holdParent.transform.position; // position
            collObj.AddComponent<CollisionChecker>(); // add Collider
        }
    }

    // Reset physics properties, and detach object from holdParent
    void DropObject()
    {
        Rigidbody heldRig = heldObj.GetComponent<Rigidbody>();
        heldRig.useGravity = true;
        heldRig.drag = 1;
        heldRig.freezeRotation = false;
        heldObj.layer = LayerMask.NameToLayer("Default");
        heldObj.GetComponent<MeshCollider>().enabled = true;
        // heldRig.isKinematic = false;
        
        heldObj.transform.parent = null;
        heldObj = null;
        holdParent.transform.position = transform.position + transform.TransformDirection(Vector3.forward)*2f;

        DestroyImmediate(collObj);
    }

    // Moves object to target position until within satisfactory threshold
    void MoveObject()
    {
        if (Vector3.Distance(heldObj.transform.position, holdParent.position) > clampFactor)
        {
            Vector3 moveDirection = (holdParent.position - heldObj.transform.position);
            heldObj.GetComponent<Rigidbody>().AddForce(moveDirection * moveForce);
        }
    }

    // Moves holdParent along view vector
    void MoveHoldParent(float dir)
    {
        // move object along view vector
        holdParent.transform.position += transform.TransformDirection(Vector3.forward)*dir;
        distanceToHeld = Vector3.Distance(holdParent.transform.position, transform.position);
    }

    // scales object according to distance from player
    void ScaleObject(GameObject obj)
    {
        distanceToHeld = Vector3.Distance(obj.transform.position, transform.position);
        float newScale = distanceToHeld * scaleFactor;
        obj.transform.localScale = new Vector3(newScale, newScale, newScale);
    }

    // Recursively navigates space to find where to project object into scene
    // return type Vector3? Transform?
    IEnumerator CollisionChecker(Vector3 startPos, Vector3 endPos)
    {
        // Get midpoint between startPos and endPos
        float totalDist = Vector3.Distance(startPos, endPos);
        Vector3 midPos = startPos + (endPos - startPos)/2;

        // Move collObj to midpoint + scale to scaleFactor
        collObj.transform.position = midPos;
        ScaleObject(collObj);

        // wait for collision triggers to update
        yield return new WaitForFixedUpdate();

        if (collObj != null)
        {
            // if this doesn't collides, recurse downward
            if (!collObj.GetComponent<CollisionChecker>().IsColliding())
            {
                StartCoroutine(CollisionChecker(midPos, endPos));
            }
            // else, reset to this recursive call's startPos
            else
            {
                collObj.transform.position = startPos;
                ScaleObject(collObj);
            }
        }
    }

    // raycast check
    void RaycastToCollision()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position,                             // ro
                            transform.TransformDirection(Vector3.forward),  // rd
                            out hit,                                        // raycast out
                            maxRayDist))                                    // max dist
        {
            // Debug.Log(hit.transform.gameObject + " " + Vector3.Distance(hit.transform.position, holdParent.transform.position));
            StartCoroutine(CollisionChecker(collObj.transform.position, hit.point));
        }
    }
} 


// TODO:
// Get distance to wall

// Check Raycast() parameters (only want to collide with walls)
    // or just ignore current heldObj
        // origin
        // direction
        // maxDistance
        // layerMask
        // queryTriggerInteraction false

// CHECK
// make collision checking public so i can call
    // maintain a boolean in collider object: isColliding
    // update on enter/exit

//on collision enter parameters (collide with walls only?)
    // actually could be fine to not do this?
    // public accessor function to return

// CHECK
// Need to be able to add script to collider object
// object.AddComponent<ScriptName>();

// using tags
    // a = GameObject.FindGameObjectWithTag("TagA).GetComponent<A>(); // gets the script?
    // distance to walls
    // ...?

// fixed distance from player (never closer than X distance)


// collision checking with all walls (some issues when swapping what raycast hits)

// increase distance of raycast
// backwards raycast logic (why doesn't get as flush too wall as possible)