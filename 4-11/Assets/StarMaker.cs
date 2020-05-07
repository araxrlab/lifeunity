using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarMaker : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string result = "";
        string empty = " ";
        for (int i = 0; i < 5; i++)
        {
            //result += empty;
            for (int k = 5; k >= i; k--)
            {
                result += empty;
            }

            for (int j = 5; j >= 5 - i; j--)
            {
                    result += "* ";
            }
            result += "\n";
            //empty += " ";
        }
        print(result);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
