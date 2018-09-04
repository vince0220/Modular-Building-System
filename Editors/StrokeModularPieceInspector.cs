#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Modular;

[CustomEditor(typeof(StrokeModularPiece),true)]
public class StrokeModularPieceInspector : ModularPieceInspector {
	private bool ConnectionsOpen = false;

	public override void OnInspectorGUI ()
	{
		Piece = (StrokeModularPiece)target; // target
		var TempPiece = (StrokeModularPiece)Piece;
		StrokeModularPiece[] Pieces = new StrokeModularPiece[targets.Length];

		RenderHeader ("Stroke Settings");
		RenderProperty<StrokeType,StrokeModularPiece> ("_StrokeType", "Stroke Type", "This variable determines what stroke type this stroke modular piece is.",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((StrokeType)EditorGUILayout.EnumPopup(GUIContent,(StrokeType)val));
		});
		RenderProperty<StrokeCategory,StrokeModularPiece> ("_StrokeCategory", "Stroke Category", "This variable determines what category this stroke piece belongs to.",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((StrokeCategory)EditorGUILayout.EnumPopup(GUIContent,(StrokeCategory)val));
		});

		if(Pieces.Length == 1){
			RenderList<Modular.ConnectionDirection> (ref TempPiece._ConnectionDirections, ref ConnectionsOpen, "Connection Directions", "", (Modular.ConnectionDirection Type, int Index) => {
				TempPiece._ConnectionDirections[Index] = (ConnectionDirection)EditorGUILayout.EnumPopup(new GUIContent("Connection: "+Index),TempPiece.ConnectionDirections[Index]);
			}, () => {
				return Modular.ConnectionDirection.Front;
			});

			if(TempPiece != null){
				RenderHeader("Stroke Debug Settings");
				EditorGUILayout.LabelField ("Current Stroke Connections: " + TempPiece.CurrentlyConnected.Length);
				if (TempPiece.CurrentlyConnected.Length > 0) {
					if (GUILayout.Button ("Draw Current Connections")) {
						TempPiece.DebugConnections (1f);
					}
				}
			}
		}



		base.OnInspectorGUI (); // draw base
	}
}
#endif