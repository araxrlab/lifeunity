using UnityEngine;
using System.Collections;

// PLEASE NOTE! THIS SCRIPT IS FOR DEMO PURPOSES ONLY :) //

public class RotateCamera : MonoBehaviour {


	void Update () {
				transform.Rotate (0, -50 * Time.deltaTime, 0);
	}
}

// PLEASE NOTE! THIS SCRIPT IS FOR DEMO PURPOSES ONLY :) //