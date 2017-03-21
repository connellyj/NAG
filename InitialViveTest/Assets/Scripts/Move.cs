using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : MonoBehaviour {
	void Update () {
        if (Input.GetKey(KeyCode.UpArrow)) transform.Translate(Vector3.forward);
        if (Input.GetKey(KeyCode.DownArrow)) transform.Translate(Vector3.back);
        if (Input.GetKey(KeyCode.RightArrow)) transform.Translate(Vector3.right);
        if (Input.GetKey(KeyCode.LeftArrow)) transform.Translate(Vector3.left);
    }
}
