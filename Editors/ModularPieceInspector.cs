#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditorInternal;

[CanEditMultipleObjects]
[CustomEditor(typeof(Modular.ModularPiece),true)]
public class ModularPieceInspector : Editor {
	private float Space = 16;
	private bool ShowHelp = false;
	protected Modular.ModularPiece Piece;
	private List<ReorderableList> TextureList = new List<ReorderableList>();
	private int TextureArrayIndex;
	private int TextureArrayTextureIndex;

	public delegate void RenderGUI(object Value,GUIContent content,RenderCallback Return);
	public delegate void RenderCallback(object returnValue);

	public void RenderHeader(string Title,bool useSpace = true){
		if (useSpace) {
			GUILayout.Space (Space);
		}
		EditorGUILayout.LabelField(Title,EditorStyles.boldLabel);
	}
	public void RenderProperty<T,P>(string VarName,string Title, string Tooltip,RenderGUI OnRender){
		T val = Piece.GetVariableValue<T>(VarName);

		OnRender (val, new GUIContent (Title, Tooltip),(returned)=>{
			if(GUI.changed){
				for (int i = 0; i < targets.Length; i++) { // set pieces
					P piece = (P)(object)targets[i];
					piece.SetVariableValue<T>(VarName,(T)returned);
				}
			}
		}); // on render

		if (ShowHelp) {
			EditorGUILayout.HelpBox (Tooltip, MessageType.Info);
		}
	}

	public void RenderProperty(string[] VarNames,bool[] Dependencies,string[] Titles, string[] Tooltips,RenderGUI[] OnRenders){
		for (int i = 0; i < VarNames.Length; i++) {
			if (Dependencies [i]) { // should render
				object val = Piece.GetVariableValue<object>(VarNames[i]); // value of var

				OnRenders[i] (val, new GUIContent (Titles[i], Tooltips[i]),(returned)=>{
					if(GUI.changed){
						for (int x = 0; x < targets.Length; x++) { // set pieces
							Modular.ModularPiece piece = (Modular.ModularPiece)targets[x];
							piece.SetVariableValue<object>(VarNames[i],returned);
						}
					}
				}); // on render

				if (ShowHelp) {
					EditorGUILayout.HelpBox (Tooltips[i], MessageType.Info);
				}
			}
		}
	}
	public void RenderList<T>(ref List<T> List,ref bool Open, string Label, string Description, System.Action<T,int> OnRender,System.Func<T> InstantiateNewItem){
		Open = EditorGUILayout.Foldout (Open, Label);

		if (Open) {
			EditorGUI.indentLevel = 1;
			int ListCount = Mathf.Max (0, EditorGUILayout.IntField ("Size: ", List.Count));
			while (ListCount < List.Count) {
				List.RemoveAt (List.Count - 1);
			}
			while (ListCount > List.Count) {
				List.Add (InstantiateNewItem());
			}


			// render items
			for (int i = 0; i < List.Count; i++) {
				OnRender (List [i],i);
			}
			EditorGUI.indentLevel = 0;
		}
	}
	public void RenderDynamicEnum(string Label, System.Enum Enum,ref int Index){
		string[] Names = System.Enum.GetNames(Enum.GetType());

		if (Names.Length > 0) {
			var Val = (System.Enum)System.Enum.Parse (Enum.GetType (), Names [0]);
			if (Names.Length > Index && Names.Length > 0) {
				Val = (System.Enum)System.Enum.Parse (Enum.GetType (), Names [Index]);
			}

			Val = EditorGUILayout.EnumPopup (Label, Val);

			// to int again
			var value = System.Enum.Parse (Val.GetType (), Val.ToString ());
			Index = System.Convert.ToInt32 (value);
		}
	}	 
	public void RenderDynamicEnum(string Label, System.Enum Enum,ref string Val){
		List<string> Names = System.Enum.GetNames(Enum.GetType()).ToList();
		if (!Names.Contains (Val) && Names.Count > 0) {Val = Names [0];}

		var Value = (System.Enum)System.Enum.Parse (Enum.GetType(),Val);
		Value = EditorGUILayout.EnumPopup(Label,Value);

		var ParseValue = System.Enum.Parse (Value.GetType(), Value.ToString());
		Val = (string)ParseValue.ToString();
	}

	private void OnEnable(){
		Modular.ModularPiece TempData = (Modular.ModularPiece)target;
		TextureList.Clear ();
		TextureList.Add(RenderList (TempData));
	}

	private ReorderableList RenderList(Modular.ModularPiece Data){
		ReorderableList List = new ReorderableList (Data.TextureArrays, typeof(ModularTextureArray.ModularTextureArraySetting), true, true, true, true);
		List.drawHeaderCallback = (Rect rect) => {
			EditorGUI.LabelField(rect,"Texture Arrays");
		};

		List.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
			if(Data.TextureArrays[index].TextureArray != null){
				float Width = rect.width;
				float ThreeFourth = Width * 0.75f;

				// draw int slider
				float HalfOfHalf = ThreeFourth * 0.5f;
				int	Max = Data.TextureArrays[index].TextureArray.Textures.Count - 1;
				EditorGUI.LabelField(new Rect(
					rect.position.x,
					rect.position.y,
					HalfOfHalf,
					rect.height
				),index+": "+Data.TextureArrays[index].TextureArray.Textures[Data.TextureArrays[index].DefaultIndex].Name);
				Data.TextureArrays[index].DefaultIndex = EditorGUI.IntSlider(new Rect(
					rect.position.x + HalfOfHalf,
					rect.position.y,
					HalfOfHalf,
					rect.height
				),Data.TextureArrays[index].DefaultIndex,0,Max);


				// Draw texture array input
				Data.TextureArrays[index].TextureArray = (ModularTextureArray)EditorGUI.ObjectField(new Rect(
					rect.position.x + ThreeFourth,
					rect.position.y,
					Width * 0.25f,
					rect.height
				),Data.TextureArrays[index].TextureArray,typeof(ModularTextureArray),false);
			} else {
				Data.TextureArrays[index].TextureArray = (ModularTextureArray)EditorGUI.ObjectField(rect,Data.TextureArrays[index].TextureArray,typeof(ModularTextureArray),false);
			}
			rect.y += 2;
		};

		return List;	
	}

	public override void OnInspectorGUI ()
	{
		Piece = (Modular.ModularPiece)target; // target
		Modular.ModularPiece[] Pieces = new Modular.ModularPiece[targets.Length];

		RenderHeader ("Modular piece boundary settings");
		RenderProperty<Vector3,Modular.ModularPiece> ("BoundSize", "Boundary size", "The Bound size variable determins the boundary extends of the modular piece. Make sure this value is as close as possible to the origional shape of the mesh.",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((Vector3)EditorGUILayout.Vector3Field(GUIContent,(Vector3)val));
		});
		RenderProperty<Vector3,Modular.ModularPiece> ("BoundsOffset", "Boundary offset", "The Bounds offset variable determines how much the center of the boundary should be offsetted compared to the pivot of the modular piece.",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((Vector3)EditorGUILayout.Vector3Field(GUIContent,(Vector3)val));
		});
		RenderProperty<bool,Modular.ModularPiece> ("AlignableItem", "Alignable Object", "Can other objects align to this object. If turn to false when fast placing items, they will pass through this object", (val,GUIContent,returnCallback) => {
			returnCallback.Invoke((bool)EditorGUILayout.Toggle(GUIContent,(bool)val));
		});


		RenderHeader ("Modular piece global settings");
		RenderProperty<Modular.ModularPiece.DirectionTypes,Modular.ModularPiece> ("UpDirection", "Up Direction", "The up direction variable determins what side of the object is Up. For example when aligning a object to a surface normal the up axis will determine what side is up.",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((Modular.ModularPiece.DirectionTypes)EditorGUILayout.EnumPopup(GUIContent,(Modular.ModularPiece.DirectionTypes)val));
		});
		RenderProperty<Modular.SnapTypes,Modular.ModularPiece> ("GridSnapType", "Grid Snap Type", "The Grid snaptype variable determins the snap type to use by default for this modular piece. Center aligns to the center. Edge alignes to the edges of the grid and freeform doesnt align. Default takes the default snap type of this modular piece.",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((Modular.SnapTypes)EditorGUILayout.EnumPopup(GUIContent,(Modular.SnapTypes)val));
		});
		RenderProperty<Modular.ModularPiece.GridCutTypes,Modular.ModularPiece> ("GridCutType", "Grid Cut Type", "Determines wheter an object should cut out of the grid or not",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((Modular.ModularPiece.GridCutTypes)EditorGUILayout.EnumPopup(GUIContent,(Modular.ModularPiece.GridCutTypes)val));
		});
		RenderProperty<Modular.ModularPiece.GridCutTypes,Modular.ModularPiece> ("GrassCutType", "Grass Cut Type", "Determines wheter an object should cut out of the grass or not",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((Modular.ModularPiece.GridCutTypes)EditorGUILayout.EnumPopup(GUIContent,(Modular.ModularPiece.GridCutTypes)val));
		});


		RenderHeader ("Modular piece grid settings");
		RenderProperty (
			new string[]{"RealMaxSize","CustomRealMaxSize"},
			new bool[]{true,(Piece.RealMaxSize == Modular.SnapAxis.Custom)},
			new string[]{"Real Max Size: "+Piece.RealScale,"Custom size"},
			new string[]{"The real max size variable determins the real maximal size of this object. You can pick a bounds axis or set a custom size.","Sets the custom maximal size of this object"},
			new RenderGUI[]{
				(val,context,returncall)=>{returncall((Modular.SnapAxis)EditorGUILayout.EnumPopup (context, (Modular.SnapAxis)val));},
				(val,context,returncall) =>{returncall((float)EditorGUILayout.Slider((float)val,0,(Piece.BoundSize.GetHighestAxis() * 2)));}
			}
		);
		RenderProperty (
			new string[]{"GridMaxSize","CustomMaxSize"},
			new bool[]{true,(Piece.GridMaxSize == Modular.SnapAxis.Custom)},
			new string[]{"Grid Max size: "+Piece.Scale,"Custom size"},
			new string[]{"The grid max variable determins the maximal grid size of this object. You can pick a bounds axis or set a custom size.","Sets the custom maximal size of this object"},
			new RenderGUI[]{
				(val,context,returncall)=>{returncall((Modular.SnapAxis)EditorGUILayout.EnumPopup (context, (Modular.SnapAxis)val));},
				(val,context,returncall) =>{returncall((float)EditorGUILayout.Slider((float)val,0,(Piece.BoundSize.GetHighestAxis() * 2)));}
			}
		);
		RenderProperty (
			new string[]{"GridMinSize","CustomMinSize"},
			new bool[]{true,(Piece.GridMinSize == Modular.SnapAxis.Custom)},
			new string[]{"Grid Min size: "+Piece.MinScale,"Custom size"},
			new string[]{"The grid min variable determins the minimal grid size of this object. You can pick a bounds axis or set a custom size.","Sets the custom minimal size of this object"},
			new RenderGUI[]{
				(val,context,returncall)=>{returncall((Modular.SnapAxis)EditorGUILayout.EnumPopup (context, (Modular.SnapAxis)val));},
				(val,context,returncall) =>{returncall((float)EditorGUILayout.Slider((float)val,0,(Piece.BoundSize.GetHighestAxis() * 2)));}
			}
		);

		RenderHeader ("Texture Array Settings");
		RenderProperty<bool,Modular.ModularPiece> ("UseTextureArray", "Use Texture Array", "Determines if you want to use a texture array or not",(val,GUIContent,returnCallback)=>{
			returnCallback.Invoke((bool)EditorGUILayout.Toggle(GUIContent,(bool)val));
		});
			
		if (Piece.UseTextureArray) {
			EditorGUILayout.Space ();
			for (int i = 0; i < TextureList.Count; i++) {
				serializedObject.Update ();
				TextureList [i].DoLayoutList ();
				serializedObject.ApplyModifiedProperties ();
			}
		} else {
			GUILayout.Space (Space);
		}

		RenderHeader ("Auto settings",false);
		#region Toggle help button
		if (GUILayout.Button (new GUIContent("Auto generate piece","Auto generates all values and auto fills the render set"),GUILayout.Width(200))) {
			for(int i = 0; i < targets.Length; i++){
				Modular.ModularPiece piece = (Modular.ModularPiece)targets[i];
				piece.AutoFillRenderSet(); // auto calc values
				piece.AutoCalculateValues(); // auto calc values
			}
		}

		RenderHeader ("Extra settings");
		if (GUILayout.Button (new GUIContent("Auto calculate values","Auto calculate all values of the modular piece. Like boundsize, min, max,etc..."),GUILayout.Width(200))) {
			for(int i = 0; i < targets.Length; i++){
				Modular.ModularPiece piece = (Modular.ModularPiece)targets[i];
				piece.AutoCalculateValues(); // auto calc values
			}
		}
		if (GUILayout.Button (new GUIContent("Auto fill RenderSet","Auto fills the render set"),GUILayout.Width(200))) {
			for(int i = 0; i < targets.Length; i++){
				Modular.ModularPiece piece = (Modular.ModularPiece)targets[i];
				piece.AutoFillRenderSet(); // auto calc values
			}
		}
		if (GUILayout.Button (new GUIContent((!ShowHelp)?"Show help boxes":"Hide help boxes","Toggles the help windows"),GUILayout.Width(200))) {
			ShowHelp = !ShowHelp;
		}
		#endregion

		SceneView.RepaintAll (); // repait scene

	}
}
#endif
