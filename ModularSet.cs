using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.ComponentModel;

namespace Modular{
	[System.Serializable]
	public class ModularSet : ModularPlacableObject,UI.UIDynamicClass,INotifyPropertyChanged {
		#region base functions
		protected override void OnAwake ()
		{
			this.Bounds = new AxisBounds (Vector3.zero,Vector3.zero,this.transform,false); // init bounds
			this.GridCenterHolder = new GameObject ("GridCenter");
			this.GridCenterHolder.transform.SetParent(this.transform); // reparent
			CalculateGridCenter (); // set grid center
			SetDefaultRestrictions(); // set default restriction values
			Management.GameManager.I.Modular.RegisterModularSet(this);
		}
		public override void OnPositionChanged ()
		{
			this.Bounds = new AxisBounds (this.BoundsCenter,this.Bounds.size,this.transform,false); // set bounds position to new changed position
			for (int i = 0; i < Pieces.Count; i++) {
				Pieces [i].OnPositionChanged (); // call on pieces to
			}
		}
		#endregion

		#region Private variables
		private string _Name = "Custom blueprint"; // name of the building
		private string _Discription = "This is the default discription of a custom modular set."; // Discription of the building
		private List<ModularPiece> _Pieces = new List<ModularPiece> ();// all modular pieces of the building
		public List<ModularPiece> Pieces{
			get{
				return _Pieces;
			}
		}
		private AxisBounds TempBounds;
		private ModularPlacableObject PlacingObject;
		private GameObject GridCenterHolder;
		private Vector3 _LastPivotPosition;
		private Vector3 _BoundsCenterOffset;
		private bool Editing = false;

		// restriction cache
		private PositionRestrictions _PositionRestriction;
		private bool _Scalable;
		private float _PositionGridRestriction;
		private float _RotationStepRestriction;
		private SpaceRestrictions _SpaceRestriction;
		private RotationRestrictions _RotationRestriction;
		#endregion				

		#region Input functions
		// publics
		public void AddModularPiece(ModularPiece piece){
			piece.transform.SetParent(transform); // reparent piece
			_Pieces.Add (piece); // add piece to set

			if (OriginPiece == piece) { // just added origin piece
				CalculateGridCenter (); // calculate grid center
			}

			UpdatePieceRestrictions(piece); // add modular piece restriction 
			UpdateModularSet(); // update modular set
				
			AddModularPieceRenderer(piece); // add to render set
		}
		public void AddModularPiece(ModularPiece piece,ModularPlacableData Trans){
			piece.Initialize (this); // initialize piece cause isnt placed by system
			piece.transform.SetParent(transform); // reparent piece
			_Pieces.Add (piece); // add piece to set

			piece.LocalPosition = Trans.LocalPosition;
			piece.transform.localEulerAngles = Trans.LocalEulerAngles;
			piece.transform.localScale = Trans.LocalScale;

			AddModularPieceRenderer(piece); // add to render set
			
			UpdatePieceRestrictions(piece); // add modular piece restriction
			
			if (Editing) {
				piece.InitializeEditable ();
			} else {
				piece.InitializePlaced (Management.GameManager.I.Modular.ModularPieceLayer);
				piece.OnPlaced ();
			} // if is in editing mode, initialize as editable piece
		}	
		public void AddModularPieceRenderer(ModularPiece Piece){
			RenderSet.AddRenderObject (Piece.RenderSet);
		}
		public void UpdateModularSet(){
			RecalculateBounds(); // recalculate bounds
		}
		public bool RemoveModularPiece(ModularPiece piece){
			if (OriginPiece == piece) { // removing origin piece from pieces
				_Pieces.Remove (piece); // remove piece from pieces
				CalculateGridCenter (); // calculate grid center
			} else {
				_Pieces.Remove (piece);// remove piece from pieces
			}

			if (!Editing && _Pieces.Count == 0) {Destroy (); return false;} // destroy self

			UpdateRestrictions (); // update all restrictions
			UpdateModularSet();
			return true;
		}
		public bool FinalizeSet(LayerMask PieceLayer,bool UpdateCenter = true){ // change modular pieces from edit to normal state, like change layers etc..
			Editing = false; // set is editing to false
			FinalizeModularPieces(PieceLayer); // finalize modular pieces

			if (Pieces.Count < 1) { // check if contains pieces
				Management.GameManager.I.Utils.ClearUndoableHistory(this.gameObject); // clear undo history
				Destroy(); // dont contain pieces destoy self
				return false; // return that is destoryed
			} else { // contain pieces
				if (UpdateCenter) {UpdateModularSet ();} // update modular set
				AutoFillRenderSet (); // auto refill render set
				this.gameObject.layer = Management.GameManager.I.Modular.ModularSetLayer.ToLayer(); // set layer
				RegisterUndoable(); // register as placed object
				return true; // is alive
			}
		}
		public ModularPlacableObject[] OverlappingPieces(ModularPlacableObject piece,float? range = null, bool CheckDefine = false){
			List<ModularPlacableObject> Overlapping = new List<ModularPlacableObject> ();
			AxisBounds b = piece.Bounds;

			for (int i = 0; i < Pieces.Count; i++) {
				ModularPlacableObject Add = piece.StackTest (Pieces [i], range, CheckDefine);
				if (Add != null) {
					Overlapping.Add (Add);
				}
			}

			return Overlapping.ToArray ();
		}
		public void AsyncLoadModularSet(ModularSetData SetData,Utils.Delegates.GenericDataCallbak InitModularPiece){
			// set base settings
			this.gameObject.name = SetData.SetName;
			this._BoundsCenterOffset = SetData.BoundsCenterOffset; // set center offset
			this.Bounds = new AxisBounds ((transform.position + _BoundsCenterOffset).ToWorldRotationFull (Bounds.LocalTransRotation, Bounds.LocalTransPosition),SetData.BoundSize,this.transform,false);
			this.GridCenterHolder.transform.localPosition = SetData.LocalGridCenter;

			// init modular pieces
			StartCoroutine (AsyncLoadPieces(SetData,InitModularPiece)); // start load coroutinte
		}
		public void InitializeEditable(){
			Editing = true;
			Management.GameManager.I.Utils.ClearUndoableHistory(this.gameObject); // clear set history
			foreach(ModularPiece piece in Pieces){piece.InitializeEditable ();} // set all pieces to editable
		}

		// privates
		private void RecalculateBounds(){
			if (Pieces.Count > 0) {
				AxisBounds b = new AxisBounds(Pieces[0].Bounds.center,Pieces[0].Bounds.size,()=>{
					// return local pos
					return Pieces[0].Bounds.center;
				},()=>{
					// return local rotation
					return this.transform.rotation;
				},()=>{
					// return local scale
					return this.transform.localScale;
				},false);

				for (int i = 1; i < Pieces.Count; i++) {
					if (Pieces[i].gameObject.activeSelf) {
						b.LocalEncapsulate (Pieces[i].Bounds);
					}
				}
				Bounds = b; // set bounds temp
				UpdatePivot(true); // update set pivot
				Bounds = new AxisBounds (Bounds.LocalCenter, b.size, ()=>{return this.transform.position;},()=>{return this.transform.rotation;},()=>{return this.transform.localScale;}, false); // set bounds to updated
			} else {
				Bounds = new AxisBounds (Vector3.zero,Vector3.zero,this.transform,false);
			}
		}
		private void FinalizeModularPieces(LayerMask PieceLayer){
			for (int i = 0; i < Pieces.Count; i++) {
				if (Pieces[i].gameObject.activeSelf) {
					Pieces[i].InitializePlaced (PieceLayer); // finalize as placed pieces
					Management.GameManager.I.Utils.ClearUndoableHistory (Pieces[i].gameObject); // clear undo history
				} else {
					Management.GameManager.I.Utils.ClearUndoableHistory (Pieces[i].gameObject,true); // clear undo history and destory item
					i--; // set i back a step
				}
			}
		}
		private void UpdateRestrictions(){
			SetDefaultRestrictions (); // set default restrictions
			for (int i = 0; i < Pieces.Count; i++) { // go through all pieces
				UpdatePieceRestrictions((ModularPlacableObject)Pieces[i]); // update modular piece restrictions
			}
		}
		private void UpdatePieceRestrictions(ModularPlacableObject Placable){
			PositionRestriction = ((int)PositionRestriction < (int)Placable.PositionRestriction)?Placable.PositionRestriction:PositionRestriction;// position restrictions
			Scalable = (!Placable.Scalable)?Placable.Scalable:Scalable; // scaling restriction
			RotationStepRestriction = (Placable.RotationStepRestriction > RotationStepRestriction)?Placable.RotationStepRestriction:RotationStepRestriction; // rotation step restrictions
			RotationRestriction = ((int)RotationRestriction < (int)Placable.RotationRestriction)?Placable.RotationRestriction:RotationRestriction;// Rotation restrictions
			SpaceRestriction = ((int)SpaceRestriction < (int)Placable.SpaceRestriction)?Placable.SpaceRestriction:SpaceRestriction;// Space restrictions

			if (Placable.PositionRestriction != PositionRestrictions.None) {
				PositionGridRestriction = (PositionGridRestriction < Placable.Scale) ? Placable.Scale : PositionGridRestriction; // set position grid restriction
			}
		}
		private void SetDefaultRestrictions(){
			// restriction cache
			_PositionRestriction = PositionRestrictions.None;
			_Scalable = true;
			_RotationStepRestriction = 0;
			_PositionGridRestriction = 0;
			_SpaceRestriction = SpaceRestrictions.None;
			_RotationRestriction = RotationRestrictions.XYZ;
		}
		public void RegisterUndoable(){
			Management.GameManager.I.Utils.RegisterInstantiate(this.gameObject,(object[] data)=>{
				Destroy(); // destory set
			},(object[] data)=>{
				// on changed
				RemoveFromGrid();
				UndoUtil.UndoData undodata = (UndoUtil.UndoData)data.Find<UndoUtil.UndoData>();
				PlaceAtPos(undodata.Position); // place last pos
				transform.rotation = undodata.Rotation;
				transform.localScale = undodata.LocalScale;
				OnUpdateGrid();
			});
		}
		public void ForEachPiece(System.Action<ModularPiece> Callback){
			for (int i = 0; i < Pieces.Count; i++) {
				Callback.Invoke (Pieces[i]);
			}
		}
		#endregion

		#region DynamicClass implementation
		public string DynamicTitle{
			get{
				return Name;
			}
		}
		#endregion

		#region INotifyPropertyChanged implementation
		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string Name){
			PropertyChangedEventHandler handler = PropertyChanged;
			if(handler != null) {
				handler (this, new PropertyChangedEventArgs(Name));
			}
		}
		#endregion

		#region Transform functions
		public void RegisterPlacingObject(ModularPlacableObject piece){
			PlacingObject = piece;
		}
		public void DeregisterPlacingObject(){
			PlacingObject = null;
		}
		public void UpdatePivot(bool TerrainY = false){
			// set last pivot position
			_LastPivotPosition = transform.position;

			List<Vector3> Vectors = new List<Vector3> ();
			Vector3 GridCenterPos = WorldGridCenter;

			// get world positions
			foreach (Transform child in transform) {
				Vectors.Add (child.transform.position);
			}

			// Get center
			Vector3 CurrentCenter = Center;
			if (TerrainY) {
				CurrentCenter.y = GetTerrainHeight (CurrentCenter);
			}

			transform.position = CurrentCenter; // set new center
			_BoundsCenterOffset = Bounds.LocalCenter - Center; // calculate bounds center offset

			// update local positions
			int count = 0;
			foreach (Transform child in transform) {
				child.transform.position = Vectors [count];
				count++;
			}

			// update grid center
			GridCenterHolder.transform.position = GridCenterPos;
		}
		private float GetTerrainHeight(Vector3 Center){
			Terrain CurrentTerrain = Terrain.activeTerrain;
			RaycastHit Hit = new RaycastHit();
			float Height = CurrentTerrain.transform.position.y + CurrentTerrain.terrainData.size.y;

			if (Physics.Raycast (new Vector3(Center.x,Height,Center.z),Vector3.down,out Hit,CurrentTerrain.terrainData.size.y * 2f,Management.GameManager.I.Constants.TerrainLayer)) {
				return Hit.point.y;
			}

			return Center.y;
		}
		private void SetLocalTransform(){
			if (OriginPiece) {SetLocalTransform (OriginPiece);}
		}
		private void SetLocalTransform(ModularPiece piece){
			if (piece != null) {
				// set rotation
				Vector3 EulerAngles = new Vector3 (0, piece.transform.eulerAngles.y, 0);

				// unparent local grid center
				GridCenterHolder.transform.SetParent (null);

				// unparent all objects
				foreach (ModularPiece p in Pieces) {
					p.transform.SetParent (null);
				}

				// make local adjustments
				this.NoneOffsettedRotation = Quaternion.Euler (EulerAngles);

				// reparent childs
				foreach (ModularPiece p in Pieces) {
					p.transform.SetParent (transform);
				}

				// reparent local grid center
				GridCenterHolder.transform.SetParent (transform);
			}
		}
		private void CalculateGridCenter(){
			if (OriginPiece != null) { // there is only one piece which is the origin piece
				GridCenterHolder.transform.position = OriginPiece.LastGridPosition;
				SetLocalTransform (); // reset local transform
			} else if (PlacingObject != null) {
				GridCenterHolder.transform.position = PlacingObject.LastGridPosition;
			} else if(Pieces.Count <= 0) {
				GridCenterHolder.transform.localPosition = Vector3.zero; // there are no pieces make grid center zero
			}
		}
		#endregion

		#region Get / setters
		public bool IsEmpty{
			get{
				return (Pieces.Count <= 0);
			}
		}
		public Vector3 Center{
			get{
				Vector3 Center = Bounds.LocalCenter;
				Center.y = Bounds.min.y; // set center y to be at lowest point

				// restrict center
				if (_PositionGridRestriction > 0 && _SpaceRestriction == SpaceRestrictions.WorldOnly) {
					// round center to be in the grid on the grid center
					Center = GlobalCalulations.RoundVectorLocal (
						_PositionGridRestriction, // the grid step size
						Center, // to round is the center position
						Vector3.forward, // forward is vector.zero cause of world space
						Vector3.zero, // grid center is vector.zero cause of world space
						new Vector3 (_PositionGridRestriction * 0.5f, 0, _PositionGridRestriction * 0.5f) // offset grid to fall in grid center. Leave Y position on zero cause y should never be offsetted
					);
				}
				return Center;
			}
		}
		public Vector3 BoundsCenter{
			get{
				return (transform.position + _BoundsCenterOffset).ToWorldRotationFull (Bounds.LocalTransRotation, Bounds.LocalTransPosition);
			}
		}
		public AxisBounds GetBoundaryIncludingPiece(ModularPiece Piece){
			if (OriginPiece == null) {
				return Piece.Bounds;
			}
			TempBounds = new AxisBounds (Bounds,false);
			TempBounds.LastMax = Bounds.LastMax;
			TempBounds.LastMin = Bounds.LastMin;
			TempBounds.LocalEncapsulate (Piece.Bounds);
			return TempBounds;
		}
		public Vector3 Bottom{
			get{
				ModularPiece Piece = OriginPiece;
				if (Piece != null) {
					Vector3 cen = Center;
					cen.y = Piece.Bottom.y;
					return cen;
				}
				return Center;
			}
		}
		public ModularPiece OriginPiece{
			get{
				foreach (ModularPiece p in Pieces) {
					if (p.DefinesBoundarys) {
						return p;
					}
				}
				return null;
			}
		}
		public Vector3 BottomMargin{
			get{
				Vector3 bot = Bottom;
				bot.y += 0.01f;
				return bot;
			}
		}
		public Vector3 GridBottom(float GridSize){
			return GlobalCalulations.RoundXandZFloat (GridSize, BottomMargin);
		}
		public Vector3 LastPivotPosition{
			get{
				return _LastPivotPosition;
			}
		}
		public ModularPieceSaveData[] PieceData{
			get{
				List<ModularPieceSaveData> Data = new List<ModularPieceSaveData> ();
				for (int i = 0; i < Pieces.Count; i++) {
					if (Pieces [i].gameObject.activeSelf) { // only add if is active
						Data.Add (new ModularPieceSaveData (Pieces [i]));
					}
				}
				return Data.ToArray ();
			}
		}
		public Vector3 BoundsCenterOffset{
			get{
				return this._BoundsCenterOffset;
			}
		}
		public Vector3 LocalGridCenter{
			get{
				return GridCenterHolder.transform.localPosition;
			}
		}
		[UI.UITextField(0,"Name your blueprint...")]
		public string Name{
			get{
				return this._Name;
			}
			set{
				this._Name = value;
				this.gameObject.name = this._Name;
				OnPropertyChanged ("Name");
			}
		}
		public string Discription{
			get{
				return this._Discription;
			}
			set{
				this._Discription = value;
			}
		}
		public Vector3 WorldGridCenter{
			get{
				return GridCenterHolder.transform.position;
			}
		}
		#endregion

		#region Virtual overrides
		public override void OnDeplaced ()
		{
			base.OnDeplaced ();
			for (int i = 0; i < Pieces.Count; i++) {Pieces [i].OnDeplaced ();} // deplace pieces
		}
		public override void OnPlaced ()
		{
			base.OnPlaced ();
			for (int i = 0; i < Pieces.Count; i++) {Pieces [i].OnPlaced ();} // place pieces
		}
		public override void OnUpdateGrid(){
			var TempBounds = new AxisBounds (Bounds,true); // set bounds position to new changed position;
			UpdateGrid (TempBounds.WorldCenter,TempBounds.WorldExtends);
		}
		public override void Destroy ()
		{
			Management.GameManager.I.Modular.RemoveModularSet (this);
			for (int i = 0; i < Pieces.Count; i++) {
				Pieces [i].OnParentDestroy (); // destroy piece
			}
			base.Destroy ();
		}
		public override ColorLink[] Colors {
			get {
				return new ColorLink[0]; // return empty color array
			}
		}
		public override Transform LocalSpace {
			get {
				if (OriginPiece != null) {
					return transform; // origin piece is determines the space of the set
				} else if (Pieces.Count > 0) {
					return Pieces [0].transform; // return first piece
				} else {
					return (PlacingObject == null) ? null : PlacingObject.transform; // the being placed item determins the space
				}
			}
		}
		public override void AutoFillRenderSet ()
		{
			RenderSet.AutoFindRenderSets (); // auto find rendersets
		}
		public override bool Editable {
			get {
				return true;
			}
		}
		public override bool CanDuplicate (out string Message)
		{
			if (Management.GameManager.I.Economy.CanSpend (Management.GameManager.I.Economy.EvaluateModularSet (this))) {
				Message = "";
				return true;
			}

			Message = "You have insufficient funds";
			return false;
		}
		public override ModularPlacableObject Duplicate ()
		{
			ModularSetStoreData SetData = new ModularSetStoreData (this);
			ModularSet NewSet = Management.GameManager.I.Modular.ModularBuildingSystem.InitNewModularSet (this);
			NewSet.AsyncLoadModularSet (SetData, (object[] Data) => {
				ModularSet Set = (ModularSet)Data[0]; // get set data
				ModularPieceSaveData PieceData = (ModularPieceSaveData)Data[1]; // get piece data
				Set.AddModularPiece(Management.GameManager.I.Modular.ModularBuildingSystem.InitStoreItem(PieceData),(ModularPlacableData)PieceData); // add store item
			});
			NewSet.gameObject.layer = this.gameObject.layer; // set layer
			NewSet.RegisterUndoable(); // register as undoable
			return NewSet;
		}

		// Restrictions
		public override SpaceRestrictions SpaceRestriction {
			get {
				return _SpaceRestriction;
			}
			set{
				_SpaceRestriction = value;
			}
		}
		public override PositionRestrictions PositionRestriction {
			get {
				return _PositionRestriction;
			}
			set {
				_PositionRestriction = value;
			}
		}
		public override bool Scalable {
			get {
				return _Scalable;
			}
			set {
				_Scalable = value;
			}
		}
		public override bool CutGrid {
			get {
				return true;
			}
		}
		public override LayerMask SelectionLayerMask {
			get {
				return Management.GameManager.I.Modular.ModularPieceLayer;
			}
		}
		public override RotationRestrictions RotationRestriction {
			get {
				return _RotationRestriction;
			}
			set {
				_RotationRestriction = value;
			}
		}
		public override float RotationStepRestriction {
			get {
				return _RotationStepRestriction;
			}
			set {
				_RotationStepRestriction = value;
			}
		}
		public override float PositionGridRestriction {
			get {
				return _PositionGridRestriction;
			}
			set {
				_PositionGridRestriction = value;
			}
		}
		#endregion

		#region IEnumerators
		private IEnumerator AsyncLoadPieces(ModularSetData SetData,Utils.Delegates.GenericDataCallbak InitModularPiece){
			int FramesWait = Mathf.CeilToInt((float)(SetData.Pieces.Length / Management.GameManager.I.Modular.LoadingFrameDuration));
			int count = FramesWait;
			for (int i = 0; i < SetData.Pieces.Length; i++) {
				ModularPieceSaveData Piece = SetData.Pieces [i];
				InitModularPiece.Invoke (new object[]{this,Piece}); // invoke callback with piece and current set
				count--;

				if (count <= 0) {
					count = FramesWait;
					yield return null;
				}
			}
			// is done with loading
			AutoFillRenderSet(); // auto fill
		}
		#endregion
	}
}
