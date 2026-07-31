// MODIFIED FOR THIS PROJECT — legacy Input calls ported to the Input System package, which is the
// only active input backend here. See the header of MenuController.cs. Re-importing the Asset
// Store package overwrites this.
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SceneController : MonoBehaviour {

    private MenuController mainMenu;

    [SerializeField,Tooltip("Input the index of your room")]
    public int roomIndex;

	// Use this for initialization
	void Start () {
        mainMenu = FindObjectOfType<MenuController>();
	}
	
	// Update is called once per frame
	void Update () {
        if (gameObject.transform.GetSiblingIndex() == 0 && !MenuController.instance.backgroundsController.GetComponent<Animation>().isPlaying)
        {

            var keyboard = Keyboard.current;
            if (keyboard == null) { return; }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                MenuController.instance.closeScenes();
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                MenuController.instance.advanceScene();
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                MenuController.instance.goBackScene();
            }

            if (keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene(roomIndex);
            }

        }

	}
}
