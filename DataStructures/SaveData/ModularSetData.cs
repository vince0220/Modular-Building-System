using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace Modular{
	[System.Serializable]
	public class ModularSetData {
		#region Data Variables
		public Vector3 BoundSize;
		public Vector3 BoundsCenterOffset;
		public Vector3 LocalGridCenter;
		public ModularPlacableData Transform;
		public ModularPieceSaveData[] Pieces;
		public string SetName;
		#endregion

		#region Private variables
		private string _Path;
		private bool _DefaultBuilding;
		private int _Price;
		#endregion

		#region Constructor
		public ModularSetData(ModularSet Set){
			Transform = new ModularPlacableData (Set.transform.position,Set.transform.rotation.eulerAngles,Set.transform.localScale); // init transform
			this.Pieces = Set.PieceData; // set pieces data
			this.SetName = Set.Name;
			this._Price = 0;
			this.BoundSize = Set.Bounds.size;
			this.LocalGridCenter = Set.LocalGridCenter;
			this.BoundsCenterOffset = Set.BoundsCenterOffset;
		}
		#endregion

		#region Get / Set
		public bool DefaultBuilding{
			get{
				return _DefaultBuilding;
			}
			set{
				_DefaultBuilding = value;
			}
		}
		#endregion
	}
}
