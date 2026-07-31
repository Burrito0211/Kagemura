// MODIFIED FOR THIS PROJECT — legacy Input calls ported to the Input System package, which is the
// only active input backend here. See the header of MenuController.cs. Re-importing the Asset
// Store package overwrites this.
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class Parallax : MonoBehaviour {

    private float x;
    public float speed;
    private Vector3 mouseX;
    private Vector3 StartPos;
    public float limitx1;
    public float limitx2;
    public bool isActive;

    // Use this for initialization
    void Start () {
        StartPos = gameObject.transform.position;
    }
	
	// Update is called once per frame
	void Update () {
        if (!isActive)
        {
            return;
        }
        //Leaving the layer where it is on a machine with no mouse, rather than snapping it to 0.
        var mouse = Mouse.current;
        if (mouse == null) { return; }

        mouseX = mouse.position.ReadValue();
        x = mouseX.x -= Screen.width / 2;
        transform.position = new Vector3 (Mathf.Clamp(x * speed, limitx1, limitx2), transform.position.y);
    }
}
