/*
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;


[CustomEditor(typeof(ModularPiece))]
public class ModularPieceEditor : Editor {
	ModularPiece piece;
	Renderer render;
	Vector3 LastSize;

	BoxBoundsHandle box;

	void OnEnable(){
		piece = (ModularPiece)target;
		render = piece.GetComponent<Renderer> ();
		box = new BoxBoundsHandle (0);

		if (piece.BoundSize == Vector3.zero) {
			piece.BoundSize = render.bounds.size;
		}

		box.size = piece.BoundSize;
	}

	public override void OnInspectorGUI ()
	{
		DrawDefaultInspector();
	}

	void OnSceneGUI(){
		if (piece.BoundSize != LastSize) {
			// value changed
			box.size = piece.BoundSize;
		}

		// update bounds
		Handles.color = new Color(107.0f / 255.0f, 189.0f / 255.0f, 214.0f / 255.0f);
		box.center = render.bounds.center;
		box.DrawHandle();
		piece.BoundSize = box.size;

		LastSize = box.size;
	}
}
#endif
*/