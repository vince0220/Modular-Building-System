using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IE;

namespace Modular{
	[System.Serializable]
	public class ModularPlacableData {
		public Vector3 LocalPosition;
		public Vector3 LocalEulerAngles;
		public Vector3 LocalScale;

		public ModularPlacableData(ModularPlacableObject ModularObject){
			this.LocalPosition = ModularObject.LocalPosition;
			this.LocalScale = ModularObject.transform.localScale;
			this.LocalEulerAngles = ModularObject.transform.localEulerAngles; 
		}
		public ModularPlacableData(ModularPiece ModularObject){
			this.LocalPosition = ModularObject.LocalPosition;
			this.LocalScale = ModularObject.transform.localScale;
			this.LocalEulerAngles = ModularObject.transform.localEulerAngles; 
		}
		public ModularPlacableData(Vector3 LocalPos, Vector3 LocalEuler, Vector3 LocalScale){
			this.LocalPosition = LocalPos;
			this.LocalEulerAngles = LocalEuler;
			this.LocalScale = LocalScale;
		}
	}
}
