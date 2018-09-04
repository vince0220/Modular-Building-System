using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Modular{
	[AddComponentMenu("Modular/Structure Piece")]
	public class StructureModularPiece : ModularPiece {
		public override bool DefinesBoundarys {
			get {
				return true;
			}
		}
		public override SnapTypes DefaultSnapType {
			get {
				return SnapTypes.Edge;
			}
		}
		public override bool DefaultCutGrid {
			get {
				return true;
			}
		}
	}
}