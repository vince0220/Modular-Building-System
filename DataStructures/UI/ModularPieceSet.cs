using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Management;

[System.Serializable]
public class ModularPieceStoreSet : ObjectInfo{
	#region Public variables
	[Header("Set settings")]
	public string Code;

	[Header("Set Items")]
	public List<ModularPieceStoreItem> Items = new List<ModularPieceStoreItem>();
	#endregion

	#region private variables
	private string ThemeID;
	#endregion
	
	#region Public input voids
	public void RegisterThemeID(string ID){
		ThemeID = ID;
	}
	public ModularPieceStoreItem FindModularStoreItem(string ID){
		for (int i = 0; i < Items.Count; i++) {
			if (Items [i].Code.ToLower () == ID.ToLower ()) {
				return Items [i];
			}
		}
		return null;
	}
	#endregion


}
