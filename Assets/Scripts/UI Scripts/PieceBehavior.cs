using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceBehavior : MonoBehaviour
{
    public Vector3 homeLocation;
    public int location;
    public float sqSize;
    public Action<Move> moveTo;

    public bool isDragged = false;
    public Camera mainCamera;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(isDragged)
        {
            if (Input.GetMouseButtonUp(0))
            {
                Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                int mouseBoardFile = Mathf.FloorToInt(mousePos.x / sqSize + 4f);
                int mouseBoardRank = Mathf.FloorToInt(mousePos.y / sqSize + 4f);
                int target = mouseBoardRank * 8 + mouseBoardFile;
                if (target != this.location)
                {
                    Debug.Log($"Move! Current Location = {location}, move to [{mouseBoardRank},{mouseBoardFile}]({target})");
                    this.moveTo(new Move(location, target, true));
                } 
                isDragged = false;
                this.transform.position = homeLocation;
                this.GetComponent<SpriteRenderer>().sortingLayerName = "Piece";
            }
            else
            {
                Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mousePos.z = 0;
                this.transform.position = mousePos;
            }
        }
    }
}
