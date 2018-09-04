using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Modular{
	public static class ModularPieceExtensions {
		public static Vector3 Bottom(this ModularPiece[] pieces,Vector3 center){
			Vector3 bottom = Vector3.zero;
			for (int i = 0; i < pieces.Length; i++) {
				if (pieces [i].Bottom.y < bottom.y || bottom == Vector3.zero) {
					bottom = pieces [i].Bottom;
				}
			}
			center.y = bottom.y;
			return center;
		}
		public static Quaternion Rotation(this ModularPiece[] pieces){
			if (pieces.Length <= 0) {
				return Quaternion.identity;
			}
			return pieces [0].transform.rotation;
		}
		public static float Scale(this ModularPiece[] pieces, float DefaultValue = 1){
			float Scale = 0;
			for (int i = 0; i < pieces.Length; i++) {
				Scale += pieces [i].Scale;
			}

			if (Scale <= 0) { // default return 1
				return DefaultValue;
			}
			return Scale / pieces.Length;
		}
		public static Vector3 Center(this ModularPiece[] pieces){
			Vector3 center = Vector3.zero;
			if (pieces.Length == 0) {
				return center;
			}

			for (int i = 0; i < pieces.Length; i++) {
				center += pieces [i].Position;
			}
			return center / pieces.Length;
		}
		public static Vector3 Center(this ModularPlacableObject[] pieces){
			Vector3 center = Vector3.zero;
			if (pieces.Length == 0) {
				return center;
			}

			for (int i = 0; i < pieces.Length; i++) {
				center += pieces [i].Position;
			}
			return center / pieces.Length;
		}
		public static ModularPlacableObject StackTest(this ModularPlacableObject Test,ModularPlacableObject Target,float? range,bool CheckDefine){
			AxisBounds b = Test.Bounds;
			if(Target.gameObject.activeSelf){
				if (range != null) {
					if (Target != Test && Target.Bounds.LocalIntersects (b) && Vector3.Distance(Target.Position,Test.Position) < range) {
						if (CheckDefine){
							if (Test.DefinesBoundarys && Test.DefinesBoundarys == Target.DefinesBoundarys || !Test.DefinesBoundarys) { // only stack if is stackable on this piece
								return Target;
							}
						} else {
							return Target;
						}
					}
				} else if (Target != Test && Target.Bounds.LocalIntersects (b)) {
					return Target;
				}
			}
			return null; // if didnt pass the tests return null
		}

		public static ConnectionDirection VectorToDirection(Vector3 Vector){
			if (Vector == Vector3.forward) {return ConnectionDirection.Front;}
			if (Vector == Vector3.back) {return ConnectionDirection.Back;}
			if (Vector == Vector3.right) {return ConnectionDirection.Right;}
			if (Vector == Vector3.left) {return ConnectionDirection.Left;}
			return ConnectionDirection.Front;
		}
		public static Vector3 DirectionToVector(ConnectionDirection Dir){
			switch (Dir) {
			case ConnectionDirection.Back:
				return Vector3.back;
				break;
			case ConnectionDirection.Right:
				return Vector3.right;
				break;
			case ConnectionDirection.Front:
				return Vector3.forward;
				break;
			case ConnectionDirection.Left:
				return Vector3.left;
				break;
			default:
				return Vector3.forward;
				break;
			}
		}
		public static Vector3[] DirectionsToVectors(ConnectionDirection[] Dirs){
			Vector3[] Vectors = new Vector3[Dirs.Length];
			for (int i = 0; i < Dirs.Length; i++) {
				Vectors [i] = DirectionToVector (Dirs[i]);
			}
			return Vectors;
		}
		public static ConnectionDirection[] VectorsToDirections(Vector3[] Vectors){
			ConnectionDirection[] Dirs = new ConnectionDirection[Vectors.Length];
			for (int i = 0; i < Vectors.Length; i++) {
				Dirs [i] = VectorToDirection (Vectors[i]);
			}
			return Dirs;
		}

		#region Stroke calculations
		// public calculations
		public static void UpdateStrokeState(Vector3 CenterPosition,float Distance,StrokeCategory Category,ModularStrokeSetData StrokeSet,System.Action<bool,StrokeType,int> Result,LayerMask Layers){
			Vector3[] Directions = StrokeConnectedDirections(CenterPosition,Distance,Category,Layers); // connection directions
			ConnectionDirection[] Connections = DirectionsToConnectionDirections(Directions); // convert to connection directions
			GetStrokeStateInformation (CenterPosition,Distance,Connections,Directions,StrokeSet,Result);
		}

		public static void GetStrokeStateInformation(Vector3 CenterPosition,float Distance,ConnectionDirection[] Connections,ModularStrokeSetData StrokeSet,System.Action<bool,StrokeType,int> Result){
			GetStrokeStateInformation (CenterPosition,Distance,Connections,DirectionsToVectors(Connections),StrokeSet,Result);
		}
		public static void GetStrokeStateInformation(Vector3 CenterPosition,float Distance,ConnectionDirection[] Connections,Vector3[] Directions,ModularStrokeSetData StrokeSet,System.Action<bool,StrokeType,int> Result){
			if (Directions.Length > 0) { // check if has any directions
				float TotalAngles = 0f; // calculate total connection angles
				for (int i = 0; i < Directions.Length; i++) { // loop through all directions
					for (int x = 0; x < Directions.Length; x++) { // loop through all directions
						if (x != i) {TotalAngles += Vector3.Angle (Directions [i], Directions [x]);}}} // calculate angle and add to total angles

				StrokeType Type = (StrokeType)(int)(TotalAngles / Directions.Length); // new stroke type
				IStrokeConnection SetPiece = StrokeSet.FindStrokePieceComponent(Type);

				if (SetPiece != null) {
					int RotationY = CalculateMatchAngle (Connections,SetPiece.ConnectionDirections.ToArray()); // calculate the angle to match
					Result.Invoke (true,Type,RotationY);
					return;
				}
			}
			Result.Invoke (false,StrokeType.Straight,0);
		}

		public static Vector3[] StrokeConnectedDirections(Vector3 CenterPosition,float Distance,StrokeCategory Category,LayerMask Layers) {
			List<Vector3> TempDirections = new List<Vector3> ();
			ForEachConnection ((IStrokeConnection Piece, Vector3 Direction) => {
				TempDirections.Add (Direction);
			},Category,Distance,CenterPosition,Layers);
			return TempDirections.ToArray();		
		}
		public static IStrokeConnection[] StrokeConnections(Vector3 CenterPosition,float Distance,StrokeCategory Category,LayerMask Layers){
			List<IStrokeConnection> TempConnections = new List<IStrokeConnection> ();
			ForEachConnection ((IStrokeConnection Piece,Vector3 Direction)=>{
				TempConnections.Add(Piece);
			},Category,Distance,CenterPosition,Layers);
			// return result as array
			return TempConnections.ToArray ();
		}
		public static int CalculateMatchAngle(ConnectionDirection[] From, ConnectionDirection[] To, int CurrentYRotation){
			return CurrentYRotation + CalculateMatchAngle (From,To);
		}
		public static int CalculateMatchAngle(ConnectionDirection[] From, ConnectionDirection[] To){
			Dictionary<int,int> Result = new Dictionary<int, int> ();
			for (int i = 0; i < From.Length; i++) {
				Vector3 F = ModularPieceExtensions.DirectionToVector (From[i]);
				for (int x = 0; x < To.Length; x++) {
					Vector3 T = ModularPieceExtensions.DirectionToVector (To[x]);
					int Angle = SignedAngle(F,T);
					if (!Result.ContainsKey (Angle)) {
						Result.Add (Angle,1);
					} else {
						Result [Angle] = Result[Angle] + 1;
					}
				}
			}
			var Highest = Result.OrderBy (kvp => kvp.Value).Last (); // find highest count
			return (int)Highest.Key;
		}

		public static void ForEachOverlapping<T> (Vector3 CheckPosition,Vector3 Extends,LayerMask Mask, System.Action<T> ForEachItem = null){
			var Colliders = Physics.OverlapBox (CheckPosition, Extends, Quaternion.identity, Mask);
			for(int i = 0; i < Colliders.Length; i++){
				T Piece = Colliders [i].GetComponent<T> ();

				if (Piece != null) {
					if (ForEachItem != null) {ForEachItem.Invoke (Piece);}
				}
			}
		}
		public static T[] CheckForOverlap<T>(Vector3 CheckPosition,Vector3 Extends,LayerMask Mask, System.Action<T> ForEachItem = null){
			List<T> Pieces = new List<T> ();
			ForEachOverlapping<T> (CheckPosition, Extends, Mask, (T Item) => {
				Pieces.Add(Item);
				ForEachItem.Invoke(Item);
			});
			return Pieces.ToArray(); // return results
		}

		// private calculations
		public static Quaternion LookRotation(Vector3 Direction){
			if (Direction == Vector3.zero) {
				return Quaternion.identity;
			}
			return Quaternion.LookRotation (Direction);
		}
		public static IStrokeConnection CheckForConnection(Vector3 CenterPosition,float Distance, StrokeCategory Category,Vector3 WorldDirection,ConnectionDirection WorldConnectionDirection, LayerMask Mask,bool ShouldDebug = false,float DebugTime = 1f){
			if (ShouldDebug) {Debug.DrawRay (CenterPosition,WorldDirection * Distance,Color.red,DebugTime);} // check if should debug
			RaycastHit[] CastResults = Physics.RaycastAll(CenterPosition,WorldDirection,Distance,Mask);
			if(CastResults.Length > 0){ // check for object
				for (int i = 0; i < CastResults.Length; i++) {
					IStrokeConnection Piece = CastResults[i].collider.transform.GetComponent<IStrokeConnection>(); // get stroke piece component of object
					if (Piece != null && Piece.StrokeCategory == Category) { // if is a stroke piece and of the same category
						// Check if has connection
						ConnectionDirection InverseWorldDirection = VectorToDirection(-WorldDirection); // calculate inverse direction
						ConnectionDirection[] WorldPieceConnections = Piece.WorldConnectionDirections; // get world connection directions of piece
						for (int x = 0; x < WorldPieceConnections.Length; x++) { // check for connection match
							if (WorldPieceConnections [x] == InverseWorldDirection) { // found a matching connection
								return Piece; // return piece
							}
						}
					}
				}
			}
			return null;
		}
		public static Vector3[] DirectionToWorldDirection(Vector3[] Directions, Quaternion LocalRotation){
			int Rotation = GlobalCalulations.RoundToWholeNumbers(90f,LocalRotation.eulerAngles.y);
			Quaternion OffsetRotation = Quaternion.Euler (0,Rotation,0); // calcualte offset rotation
			for (int i = 0; i < Directions.Length; i++) {Directions [i] = OffsetRotation * Directions [i];} // offset vectors
			return Directions;
		}
		public static ConnectionDirection[] ConnectionToWorldConnection(ConnectionDirection[] ConnectionDirections, Quaternion LocalRotation){
			int Rotation = GlobalCalulations.RoundToWholeNumbers(90f,LocalRotation.eulerAngles.y);
			Quaternion OffsetRotation = Quaternion.Euler (0,Rotation,0); // calcualte offset rotation
			Vector3[] Vectors = ModularPieceExtensions.DirectionsToVectors (ConnectionDirections.ToArray()); // convert to vector3 array
			for (int i = 0; i < Vectors.Length; i++) {Vectors [i] = OffsetRotation * Vectors [i];} // offset vectors
			return ModularPieceExtensions.VectorsToDirections (Vectors);
		}

		private static int SignedAngle(Vector3 a, Vector3 b){
			var angle = Vector3.Angle (a, b);
			return (int)(angle * Mathf.Sign (Vector3.Cross (b,a).y));
		}
		public static void ForEachConnection(System.Action<IStrokeConnection,Vector3>Event,StrokeCategory Category,float Distance,Vector3 CenterPosition, LayerMask Layers){
			// check front,back,left,right
			IStrokeConnection TopPiece = CheckForConnection(CenterPosition,Distance,Category,Vector3.forward,ConnectionDirection.Front,Layers,true);
			IStrokeConnection BottomPiece = CheckForConnection(CenterPosition,Distance,Category,-Vector3.forward,ConnectionDirection.Back,Layers,true);
			IStrokeConnection RightPiece = CheckForConnection(CenterPosition,Distance,Category,Vector3.right,ConnectionDirection.Right,Layers,true);
			IStrokeConnection LeftPiece = CheckForConnection(CenterPosition,Distance,Category,-Vector3.right,ConnectionDirection.Left,Layers,true);

			if (TopPiece != null) {Event.Invoke (TopPiece,Vector3.forward);}
			if (BottomPiece != null) {Event.Invoke (BottomPiece,-Vector3.forward);}
			if (RightPiece != null) {Event.Invoke (RightPiece,Vector3.right);}
			if (LeftPiece != null) {Event.Invoke (LeftPiece,-Vector3.right);}
		}
		public static HashSet<IStrokeConnection> UpdateCurrentlyConnected(this IStrokeConnection Self,HashSet<IStrokeConnection> CurrentlyConnected,float CheckDistance,Vector3 CenterPosition,LayerMask Layers){
			HashSet<IStrokeConnection> TempConnections = new HashSet<IStrokeConnection> ();
			ModularPieceExtensions.ForEachConnection ((IStrokeConnection Connection,Vector3 Pos)=>{
				TempConnections.Add(Connection);
				if(!CurrentlyConnected.Contains(Connection)){ // new connection wasnt in last connections meaning its added
					Connection.OnSurroundingAdded(Self);}
			},Self.StrokeCategory,CheckDistance,CenterPosition,Layers);

			foreach(IStrokeConnection Connection in CurrentlyConnected){
				if (!TempConnections.Contains (Connection)) { // old connection is not in the new connections anymore meaning its gone
					Connection.OnSurroundingRemoved(Self);}
			}

			return TempConnections; // set temp connections
		}
		public static void RemoveSelfFromConnections(this IStrokeConnection Self){
			IStrokeConnection[] CurrentlyConnected = Self.CurrentlyConnected;
			for (int i = 0; i < CurrentlyConnected.Length; i++) {
				CurrentlyConnected [i].OnSurroundingRemoved (Self);
			}
		}
		public static T GetInterface<T> (this GameObject obj) where T : class{
			return obj.GetComponents<Component> ().OfType<T>().FirstOrDefault();
		}
		private static ConnectionDirection[] DirectionsToConnectionDirections(Vector3[] Directions){
			List<ConnectionDirection> Dirs = new List<ConnectionDirection> ();
			for (int i = 0; i < Directions.Length; i++) {
				Dirs.Add (ModularPieceExtensions.VectorToDirection(Directions[i]));
			}
			return Dirs.ToArray ();
		}
		#endregion
	}

	public enum StrokeCategory{
		Path,
		Fence
	}
	public enum StrokeType{
		Straight = 180,
		Turn = 90,
		ThreeWay = 240,
		Cross = 360,
		End = 0
	}
	public enum SnapTypes{
		FreeForm,
		Center,
		Edge,
		Cross,
		Default
	}
	public enum ConnectionDirection{
		Front,
		Back,
		Left,
		Right
	}
	public enum SpaceRestrictions{
		None = 1,
		WorldOnly = 2
	}
	public enum PositionRestrictions{
		None = 1,
		GridOnly = 2
	}

	public enum StackTypes{
		Boundary,
		Center,
		Disabled
	}
	public enum RotationRestrictions{
		XYZ = 1,
		Y = 2

	}
	public enum SnapAxis{
		X,
		Y,
		Z,
		Custom
	}
}
