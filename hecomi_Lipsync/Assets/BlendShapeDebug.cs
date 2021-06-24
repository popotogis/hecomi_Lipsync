using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

public class FaceChange : MonoBehaviour
{

	private VRMBlendShapeProxy proxy;

	// Use this for initialization
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{
		if (proxy == null)
		{
			proxy = GetComponent<VRMBlendShapeProxy>();
		}
		else
		{
			//キーボード入力
			if (Input.GetKey(KeyCode.A))
			{
				//表情呼び出し
				proxy.SetValue("A", 1.0f);
			}
			else
			{
				proxy.SetValue("A", 0f);
			}

			if (Input.GetKey(KeyCode.I))
			{
				proxy.SetValue("I", 1.0f);
			}
			else
			{
				proxy.SetValue("I", 0f);
			}

			if (Input.GetKey(KeyCode.U))
			{
				proxy.SetValue("U", 1.0f);
			}
			else
			{
				proxy.SetValue("U", 0f);
			}

			if (Input.GetKey(KeyCode.E))
			{
				proxy.SetValue("E", 1.0f);
			}
			else
			{
				proxy.SetValue("E", 0f);
			}

			if (Input.GetKey(KeyCode.O))
			{
				proxy.SetValue("O", 1.0f);
			}
			else
			{
				proxy.SetValue("O", 0f);
			}
		}
	}
}