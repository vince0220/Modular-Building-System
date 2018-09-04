using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Management;
using DreamInCode.BuildingSystem;
using System.ComponentModel;
using System;

namespace Modular.States{
	public class StrokePlacableObject : IControllableState {
		#region Base variables
		private ModularBuildingSystem System;
		#endregion

		#region Private variables
		private System.Func<StrokeType,ModularPlacableObject> ModularInfoGetter;
		private System.Func<StrokeType,ModularPlacableObject> ModularInstanceGetter;
		private int StrokeState;
		private Vector3 StrokeStartPos;
		private float Distance;
		private RenderSet RenderSet;
		private StrokeModularPiece OriginPlacable;
		private ModularStrokeSetData OriginSetData;
		private List<GameObject> HiddenPieces = new List<GameObject> ();
		private SceneHandle[] SquarePointers;
		private RenderSet CrossRenderSet;
		private System.Action<ModularPlacableObject> OnPlaced;
		private System.Action<ModularPlacableObject> OnDeleted;
		private System.Action<object[]> OnCanceledCallback;
		private System.Func<int,bool> CanPlace;
		#endregion

		#region IState implementation
		public void Enter (List<object> GenData = null)
		{
			System = GenData.Find<ModularBuildingSystem> (true); // create link to Modular building system
			var Getters = GenData.Find<System.Func<StrokeType,ModularPlacableObject>[]>(true);
			OriginSetData = GenData.Find<ModularStrokeSetData>(true);
			OnCanceledCallback = GenData.Find<System.Action<object[]>>(true); // set on cancle callback

			var PlacedDelete = GenData.Find<System.Action<ModularPlacableObject>[]> (true);
			OnPlaced = PlacedDelete [0];
			OnDeleted = PlacedDelete[1]; // set on cancle callback
			CanPlace = GenData.Find<System.Func<int,bool>>(true);
			ModularInfoGetter = Getters [0];
			ModularInstanceGetter = Getters [1];
			OriginPlacable = (StrokeModularPiece)ModularInfoGetter (StrokeType.Straight);
			Distance = OriginPlacable.Scale;

			// set renderset
			RenderSet = OriginPlacable.RenderSet;
			CrossRenderSet = Management.GameManager.I.Modular.CrossPrefab.GetComponent<RenderSet> ();

			Init ();
		}
		public void Update ()
		{
			CheckUserInput (); // check user input

			// Position First Handle
			if (StrokeState == 1) { // draw
				// Hide last time hidden items
				for (int i = 0; i < HiddenPieces.Count; i++) {
					HiddenPieces [i].gameObject.SetActive (true);
				}
				HiddenPieces.Clear ();

				Vector4[] Points = ForEachStrokePoint ((Vector4 Position, StrokeType Type, Quaternion Rotation) => {
					if(PlacableFilter(Position)){
						RenderObjectAt (Position, Rotation, OriginSetData.FindStrokePieceComponent (Type).RenderSet);
					} else {
						RenderObjectAt (Position, Rotation,new Vector3(OriginPlacable.Scale / 4f,OriginPlacable.Scale / 4f,OriginPlacable.Scale / 4f), CrossRenderSet);
					}
				}, (GameObject Overlapping) => {
					Overlapping.gameObject.SetActive (false);
					HiddenPieces.Add (Overlapping);
				});
				SquarePointers [1].transform.position = Points [Points.Length - 1];
				SquarePointers [0].transform.position = Points [0];
			} else {
				SquarePointers[0].transform.position = MousePosition;
			}
		}
		public void Interrupt ()
		{
			
		}
		public void ForceExit ()
		{
			
		}
		public void Exit ()
		{
			CancelStroke ();
			DestroyObjects ();
			GameManager.I.Utils.EnableLayer(UtilityController.ProcessLayer.Selection);
		}
		public void ReEnterExit ()
		{
			Exit ();
		}
		#endregion

		#region private functions

		#region Init functions
		private void DestroyObjects(){
			for (int i = 0; i < SquarePointers.Length; i++) {
				GameObject.Destroy (SquarePointers[i].gameObject);}
		}
		private void Init(){
			GameManager.I.Utils.DisableLayer(UtilityController.ProcessLayer.Selection);

			InitHandles ();
		}
		private void InitHandles(){
			SquarePointers = new SceneHandle[2];

			for (int i = 0; i < SquarePointers.Length; i++) {
				SquarePointers [i] = GameObject.Instantiate (Management.GameManager.I.Modular.SquarePointerPrefab).GetComponent<SceneHandle>();
				SquarePointers [i].gameObject.SetActive (false);}

			SquarePointers [0].gameObject.SetActive (true);
		}
		private void InitExit(){
			GameManager.I.Utils.EnableLayer (UtilityController.ProcessLayer.Selection);
		}
		#endregion

		#region User input
		private void CheckUserInput(){
			if (Management.GameManager.I.Utils.IsEnabled (UtilityController.ProcessLayer.SceneHUD)) {
				if (Input.GetMouseButtonDown (0)) {
					LeftMousePress ();
				} else if (Input.GetKeyDown(KeyCode.Escape)) {
					RightMousePress ();
				}
			}
		}
		private void LeftMousePress(){
			if (StrokeState == 1) { // if current stroke is placed
				OnPlace ();
			} else {
				SquarePointers [1].gameObject.SetActive (true);
				StrokeState = 1; // set stroking state to 1
				StrokeStartPos = MousePosition;
			}
		}
		private void RightMousePress(){
			if (StrokeState != 0) {
				CancelStroke ();
			} else {
				OnCanceledCallback.Invoke (new object[]{});
			}
		}
		private void CancelStroke(){
			StrokeState = 0; // set stroking state to 0
			for (int i = 0; i < HiddenPieces.Count; i++) {
				HiddenPieces [i].gameObject.SetActive (true);
			}
			HiddenPieces.Clear ();
			SquarePointers [1].gameObject.SetActive (false);
		}
		private void OnPlace(){
			if(CanPlace.Invoke(StrokeCount)){
				for (int i = 0; i < HiddenPieces.Count; i++) {HiddenPieces [i].gameObject.SetActive (true);}
				HiddenPieces.Clear ();

				Dictionary<Vector4,ModularPlacableObject> NewlyPlaced = new Dictionary<Vector4, ModularPlacableObject> ();
				ForEachStrokePoint ((Vector4 Position,StrokeType Type,Quaternion Rotation)=>{
					if(PlacableFilter(Position)){
						NewlyPlaced.Add(Position,PlaceObjectAt(Position,Rotation,Type));
					}
					StrokeStartPos = Position; // set Stroke start pos to be last
				},(GameObject Overlapping)=>{
					var ModularPlacable = Overlapping.GetComponent<ModularPlacableObject>();
					if(ModularPlacable != null){
						OnDeleted.Invoke(Overlapping.GetComponent<ModularPlacableObject>());
					} // invoke on deleted callback
					ModularPlacable.gameObject.SetActive(false);
					ModularPlacable.Destroy();
				},(Vector4 Position,StrokeType Type,Quaternion Rotation)=>{ // for each newly placed item
					if(NewlyPlaced.ContainsKey(Position)){
						OnPlaced.Invoke (NewlyPlaced[Position]);
					}
				});

				foreach (var Placed in NewlyPlaced) {Placed.Value.OnPlaced ();} // on placed event
			}
		}
		#endregion

		#region Functionality
		private void RenderObjectAt(Vector3 Position, Quaternion Rotation,Vector3 Scale, RenderSet Set){
			RenderObjectAt (new Vector3[]{Position},Rotation,Scale,Set);
		}
		private void RenderObjectAt(Vector3 Position, Quaternion Rotation, RenderSet Set){
			RenderObjectAt (new Vector3[]{Position},Rotation,Set.gameObject.transform.localScale,Set);
		}
		private void RenderObjectAt(Vector3 Position, Vector3 Direction, RenderSet Set){
			RenderObjectAt (new Vector3[]{Position},Direction,Set);
		}
		private void RenderObjectAt(Vector3[] Positions, Vector3 Direction,RenderSet Set){
			Quaternion Rotation = Quaternion.identity;
			if (Direction != Vector3.zero) {Rotation = Quaternion.LookRotation (Direction);} // init rotation
			RenderObjectAt (Positions,Rotation,Set.gameObject.transform.localScale,Set);
		}
		private void RenderObjectAt(Vector3[] Positions, Quaternion Rotation,Vector3 Scale,RenderSet Set){
			BrokenArray<Matrix4x4>[] TempMatrices = new BrokenArray<Matrix4x4>[Set.Sets.Count]; // initialize matrices array

			for (int i = 0; i < Positions.Length; i++) {
				for (int x = 0; x < TempMatrices.Length; x++) {
					if (TempMatrices [x] == null) {TempMatrices [x] = new BrokenArray<Matrix4x4>(Positions.Length,1023);} // init broken array if isnt there
					RenderSet.Set CurrentSet = Set.Sets[x]; // get current renderset
					Transform SetTransform = CurrentSet.ParentSet.transform; // get transform of current set

					Matrix4x4 Matrix = new Matrix4x4 (); // init new matrix
					Vector3 Point = Positions [i] + (SetTransform.position - Set.gameObject.transform.position); // calculate position
					Point = RotatePointAroundPivot(Point,Positions [i],Rotation); // offset point by rotation

					Matrix.SetTRS (Point,Rotation * SetTransform.rotation,Scale); // set matrix transform
					TempMatrices [x][i] = Matrix; // set matrix
				}
			}

			// render matrixes
			for(int i = 0; i < TempMatrices.Length; i++){ // for every matrix
				MaterialPropertyBlock Properties = new MaterialPropertyBlock(); // init new property block
				Set.Sets [i].Renderer.GetPropertyBlock (Properties); // load in properties

				if (TempMatrices [i] != null && TempMatrices[i].Count > 0) { // only try to draw if is not null
					for (int x = 0; x < TempMatrices [i].PatchAmount; x++) { // for every patch
						Graphics.DrawMeshInstanced (Set.MeshFilters [i].sharedMesh, 0, Set.Sets [i].Renderer.sharedMaterial, TempMatrices [i].Patches [x],TempMatrices [i].Patches [x].Length,Properties); // draw patch with property block
					}
				}
			}
		}

		private ModularPlacableObject PlaceObjectAt(Vector3 Position, Quaternion Rotation,StrokeType Type){
			ModularPlacableObject Placable = ModularInstanceGetter.Invoke (Type); // get placable
			Placable.transform.rotation = Rotation; // set rotation
			Placable.Position = Position; // set position
			return Placable;
		}
		#endregion

		#region Calculations
		private bool PlacableFilter(Vector4 Position){
			Vector3 Extends = new Vector3 (OriginPlacable.MinScale, OriginPlacable.MinScale, OriginPlacable.MinScale); // check extends
			bool ConnectionPointOverlap = false;
			ModularPieceExtensions.ForEachOverlapping<ModularConnectionPoint>(new Vector3(Position.x,Position.y,Position.z),Extends,GameManager.I.Modular.ModularPieceTempLayer + GameManager.I.Modular.ModularPieceLayer,(ModularConnectionPoint Point)=>{
				ConnectionPointOverlap = true;
			});

			return (Mathf.Round(Position.w) <= 0f && !ConnectionPointOverlap);
		}
		private Vector4[] ForEachStrokePoint(System.Action<Vector4,StrokeType,Quaternion> ForEachPoint,System.Action<GameObject> ForEachOverlapping, System.Action<Vector4,StrokeType,Quaternion> ForEachNewlyPlaced = null){
			// Step 1 calculate snap points
			Vector4[] SnapPoints = CalculateWorldSnapPositions (StrokeStartPos, MousePosition, Distance,(Vector4 Vector)=>{
				return Vector;
			}, true, GameManager.I.Constants.TerrainLayer);

			// Step 2 Calculate initial data
			Vector3 RealDirection = (SnapPoints [SnapPoints.Length - 1] - SnapPoints [0]).normalized;
			RealDirection.y = 0;
			Quaternion Rotation = ModularPieceExtensions.LookRotation(RealDirection);

			// Step 3 calculate surrounding data
			Vector3[] SurroundingData = new Vector3[(SnapPoints.Length * 3) + 2]; // init position temp array
			bool[] SurroundingBools = new bool[(SnapPoints.Length * 3) + 2]; // init bool temp array
			UpdateConnection (SnapPoints,Rotation,ref SurroundingData,ref SurroundingBools); // update connection arrays

			// Step 4 - 5 hide overlapping pieces / calculate draw data
			for (int i = 0; i < SnapPoints.Length; i++) {
				int PointIndex = 2 + (i * 3);
				bool Overlapping = false;

                // invoke
				ModularPieceExtensions.ForEachOverlapping<StrokeModularPiece>(SnapPoints[i],new Vector3(OriginPlacable.MinScale,OriginPlacable.MinScale,OriginPlacable.MinScale),GameManager.I.Modular.ModularPieceTempLayer + GameManager.I.Modular.ModularPieceLayer,(StrokeModularPiece Piece)=>{
					if(Piece.StrokeCategory == OriginPlacable.StrokeCategory){
						ForEachOverlapping.Invoke(Piece.gameObject); // invoke overlapping item
						if(!Overlapping) {Overlapping = true;} // is overlapping
					} 
				});	
				ModularPieceExtensions.GetStrokeStateInformation (SnapPoints [i],OriginPlacable.Scale,GetConnectionsOfPoint(PointIndex,Rotation,ref SurroundingBools),OriginSetData,(bool Could,StrokeType Type,int YRotation)=>{
					ForEachPoint.Invoke(SnapPoints[i],Type,Quaternion.Euler(0,YRotation,0));
					if(!Overlapping && ForEachNewlyPlaced != null){ForEachNewlyPlaced.Invoke(SnapPoints[i],Type,Quaternion.Euler(0,YRotation,0));}
				});
			}
			return SnapPoints;
		}

		private ConnectionDirection[] GetConnectionsOfPoint(int Index,Quaternion Rotation, ref bool[] SurroundingBools){
			List<ConnectionDirection> Connections = new List<ConnectionDirection> ();

			// Add connections
			if (SurroundingBools [Index + 1]) {Connections.Add (ConnectionDirection.Left);} // Add Left
			if (SurroundingBools [Index - 1]) {Connections.Add (ConnectionDirection.Right);} // Add Right
			if (SurroundingBools [Mathf.Clamp(Index + 3,0,SurroundingBools.Length - 1)]) {Connections.Add (ConnectionDirection.Front);} // Add Up
			if (SurroundingBools [Mathf.Clamp(Index - 3,0,SurroundingBools.Length - 1)]) {Connections.Add (ConnectionDirection.Back);} // Add Down

			return ModularPieceExtensions.ConnectionToWorldConnection(Connections.ToArray (),Rotation);
		}

		private void UpdateConnection(Vector4[] SnapPoints,Quaternion Rotation,ref Vector3[] SurroundingPoints, ref bool[] SurroundingBools){
			// update lists
			for (int i = 0; i < SnapPoints.Length; i++) {
				Vector3 PointPosition = SnapPoints[i];
				int PointIndex = 2 + (i * 3);
				SurroundingBools [PointIndex] = PlacableFilter(SnapPoints[i]); // set base point

				bool[] LeftRightConnections = CheckForConnection (PointPosition,new Vector3[]{Vector3.left,Vector3.right},Rotation);
				SurroundingBools [PointIndex + 1] = LeftRightConnections [0];
				SurroundingBools [PointIndex - 1] = LeftRightConnections [1];
			}

			SurroundingBools [0] = CheckForConnection (SnapPoints[0],Vector3.back,Rotation); // update first
			SurroundingBools [SurroundingBools.Length - 1] = CheckForConnection(SnapPoints[SnapPoints.Length - 1],Vector3.forward,Rotation); // update last
		}
		
		private bool[] CheckForConnection(Vector3 Point,Vector3[] Directions,Quaternion Rotation){
			Point.y += 0.05F;
			Vector3[] CheckDirections = ModularPieceExtensions.DirectionToWorldDirection (Directions,Rotation); // calculate check vectors
			ConnectionDirection[] CheckConnections = ModularPieceExtensions.VectorsToDirections (CheckDirections); // calculate check connections
			bool[] Results = new bool[CheckDirections.Length];

			for (int i = 0; i < Results.Length; i++) { // check for connections
				Results[i] = ModularPieceExtensions.CheckForConnection(Point,OriginPlacable.Scale,OriginPlacable.StrokeCategory,CheckDirections[i],CheckConnections[i],GameManager.I.Modular.ModularPieceTempLayer + GameManager.I.Modular.ModularPieceLayer) != null;
			}
			
			return Results;
		}
		private bool CheckForConnection(Vector3 Point, Vector3 Direction,Quaternion Rotation){
			return CheckForConnection (Point,new Vector3[]{Direction},Rotation)[0];
		}

		private Vector3 RotatePointAroundPivot(Vector3 Point, Vector3 Pivot, Quaternion Rotation){
			return (Rotation * (Point - Pivot)) + Pivot;
		}
		private Vector4[] CalculateWorldSnapPositions(Vector3 Start, Vector3 End,float SnapDistance,System.Func<Vector4,Vector4> Filter = null,bool AlignToTerrain = false, LayerMask TerrainLayer = default(LayerMask)){
			Vector3 SnapDirection = CalculateSnapDirection (Start, End, SnapDistance);
			float Distance = SnapDirection.magnitude; // calculate distance between start and end
			SnapDirection = SnapDirection.normalized;
			return CalculateSnapPositions(Start,SnapDirection,Distance,SnapDistance,Filter,AlignToTerrain,TerrainLayer); // calculate snap positions
		}
		private Vector4[] CalculateSnapPositions(Vector3 Start, Vector3 End,float SnapDistance,System.Func<Vector4,Vector4> Filter = null,bool YAxis = false, bool AlignToTerrain = false, LayerMask TerrainLayer = default(LayerMask)){
			Vector3 Direction = End - Start; // calculate gradient between start and end
			if(!YAxis){Direction.y = 0f;} // if not y axis cancel out y axis
			float Distance = Direction.magnitude; // calculate distance between start and end
			Direction = Direction.normalized; // calculate normalized direction
			return CalculateSnapPositions(Start,Direction,Distance,SnapDistance,Filter,AlignToTerrain,TerrainLayer); // calculate snap positions
		}
		private Vector4[] CalculateSnapPositions(Vector3 Start,Vector3 Direction, float Distance, float SnapDistance,System.Func<Vector4,Vector4> Filter = null,bool AlignToTerrain = false, LayerMask TerrainLayer = default(LayerMask)){
			int Steps = Mathf.Clamp(Mathf.RoundToInt ((Distance / SnapDistance)),0,int.MaxValue) + 1; // calculate how many steps with the given snap distance fit in the distance
			if(Steps > 0){ // if there are any steps
				Terrain ActiveTerrain = Terrain.activeTerrain;
				Vector3 TerrainPosition = ActiveTerrain.transform.position;
				float Height = ActiveTerrain.terrainData.size.y;

				Vector4[] SnapPositions = new Vector4[Steps]; // init new snap position array with the length of the total amount of steps
				for (int i = 0; i < Steps; i++) { // loop through each step to calculate its new position
					SnapPositions [i] = Start + (Direction * (SnapDistance * i)); // calculate the step position
					if(Filter != null){SnapPositions [i] = Filter.Invoke (SnapPositions[i]);} // if there is a filter use it
					if (AlignToTerrain) { // align position to the terrain
						RaycastHit Hit = new RaycastHit(); // init new raycast hit info
						if(Physics.Raycast(new Vector3(SnapPositions[i].x,TerrainPosition.y + Height,SnapPositions[i].z),Vector3.down,out Hit,(Height*2),TerrainLayer)){ // raycast down for terrain
							SnapPositions [i].y = Hit.point.y;
							SnapPositions [i].w = CalculateAngle (Vector3.up, Hit.normal);
						} // hit the terrain
					}
				}
				return SnapPositions; // return results
			}

			return new Vector4[0]; // return empty result array
		}
		private Vector3 CalculateSnapDirection(Vector3 Start, Vector3 End, float SnapDistance, bool Normalized = false, bool YAxis = true){
			Vector3 Direction = End - Start; // calculate gradient between start and end
			if (Mathf.Abs(Direction.x) >= Mathf.Abs(Direction.z)) { // calculate wheter to take diretion x or z
			Direction = new Vector3 (Direction.x,0,0);} else {
				Direction = new Vector3 (0,0,Direction.z);}
			if (Normalized) {Direction = Direction.normalized;} // check if should be normalized
			if(!YAxis){Direction.y = 0f;} // if should not return y axis
			return Direction; // return normalized direction
		}
		private float CalculateAngle(Vector3 A, Vector3 B){
			return Mathf.Acos ((A.x * B.x) + (A.y * B.y) + (A.z * B.z)) * Mathf.Rad2Deg;
		}
		#endregion
		#endregion

		#region Get / Set
		private int StrokeCount{
			get{
				return Mathf.Clamp(Mathf.RoundToInt ((Distance / Distance)),0,int.MaxValue) + 1; // calculate how many steps with the given snap distance fit in the distance
			}
		}
		private Vector3 StrokeDirection{
			get{
				Vector3 Direction = (MousePosition - StrokeStartPos).normalized;
				Direction.y = 0;
				return Direction;
			}
		}
		private Quaternion StrokeRotation{
			get{
				return Quaternion.LookRotation (StrokeDirection);
			}
		}
		private Vector3 MousePosition{
			get{
				SelectionHitOBJ HitObj = SelectionSystem.getMouseFowardLocation (new GameObject[]{},true, GameManager.I.Constants.TerrainLayer);
				return OriginPlacable.CalculateRestrictedPosition(HitObj.HitPos,OriginPlacable.SnapType);
			}
		}
		#endregion
	}
}
