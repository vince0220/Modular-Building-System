using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.ComponentModel;
using Tracker.Grids;

namespace Modular{
	[System.Serializable]
	[RequireComponent(typeof(Utils.RenderSet))] // require renderset by default
	public class ModularPlacableObject : MonoBehaviour, IUndoable,IDynamicObstacle{
		#region Protected variables
		protected AxisBounds _Bounds;
		#endregion

		#region Variables
		private bool _GridPosChanged;
		private Vector3 _LastGridPosition;
		private Vector3 _LastPosition;
		private float _LastStackY;
		private Quaternion _RotationOffset;
		private Quaternion _NoneOffsettedRotation;
		private Utils.RenderSet _RenderSet;
		private List<ColorLink> _ColorLinks = new List<ColorLink>();
		private List<TextureLink> _TextureLinks = new List<TextureLink>();
		#endregion

		#region Base functions
		private void Awake(){
			OnAwake ();
		}
		private void Start(){
			OnStart ();
		}
		#endregion

		#region Input Function
		// place at position functions
		public void PlaceAtPos(Vector3 Pos,Vector3 Offset = default(Vector3),bool UpdateLastPlaced = true){
			PlaceAtPos (Pos,SnapTypes.FreeForm,null,Offset,UpdateLastPlaced);
		}
		public void PlaceAtPos(Vector3 Pos,SnapTypes Snap,Vector3? AlignNormal,Transform Trans=null,Vector3 Offset = default(Vector3),bool UpdateLastPlaced = true){
			PlaceAtPos (Pos, Snap,Trans,Offset,UpdateLastPlaced);
			
			if (AlignNormal != null) {
				AlignToSurface ((Vector3)AlignNormal);
			}
		}
		public void PlaceAtPos(Vector3 Pos,SnapTypes Snap,Transform Trans=null,Vector3 Offset = default(Vector3),bool UpdateLastPlaced = true){
			Vector3 FinalPos = Pos; // temp final position to be edited in this function in order to keep the origional mouse Pos intact
			Trans = (SpaceRestriction == SpaceRestrictions.WorldOnly)? null : Trans; // check space restriction
			Snap = RestrictSnapType(Snap); // restrict snap type
			Quaternion Rotation = (Trans)? Trans.rotation:Quaternion.identity;
			FinalPos = CalculateRestrictedPosition (FinalPos, Snap, Trans); // calculate restricted position

			if(UpdateLastPlaced){
				UpdateLastGridPosition (FinalPos,Pos,Rotation,Snap); // update Last GridPosition
			}

			Position = FinalPos + Offset; // set position
			LastPosition = Position; // Update last position

			if(GridPosChanged){ // check if grid position changed
				OnPositionChanged (); // call on position changed
			}
		}

		// Auto stack functions
		public void AutoStack(StackTypes StackType, System.Func<ModularPlacableObject[]> OverLapping){
			if (StackType != StackTypes.Disabled) {
				if (GridPosChanged) {
					while (AutoStackSingle (OverLapping ())) {}
					_LastStackY = Position.y;
				}
				PlaceAtPos(new Vector3 (Position.x,_LastStackY,Position.z),Vector3.zero,false); // place at position. Dont update last grid position. It will override the actual last grid position to the free from one
			}
		}
		public void AutoStack(StackTypes StackType,LayerMask Layer){
			if (StackType != StackTypes.Disabled) {
				if (GridPosChanged) {
					while (AutoStackSingle (OverlappingObjects (Layer, (StackType == StackTypes.Boundary) ? null : (float?)this.MinScale, true))) {
					} // stack until stacking is no longer possible
					_LastStackY = Position.y; // set last stack position
				}
				PlaceAtPos(new Vector3 (Position.x, _LastStackY, Position.z),Vector3.zero,false); // place at position. Dont update last grid position. It will override the actual last grid position to the free from one
			}
		}
		public void AutoStack(StackTypes StackType, ModularSet ParentSet){
			if (StackType != StackTypes.Disabled) {
				if (GridPosChanged) { // only stack if grid pos has changed
					while (AutoStackSingle (ParentSet.OverlappingPieces (this, (StackType == StackTypes.Boundary) ? null : (float?)this.MinScale, true))) {
					} // stack until stacking is no longer possible
					_LastStackY = Position.y; // set last stack position
				} else {
					PlaceAtPos(new Vector3 (Position.x, _LastStackY, Position.z),Vector3.zero,false); // place at position. Dont update last grid position. It will override the actual last grid position to the free from one
				}
			}
		}
		public bool AutoStackSingle(ModularPlacableObject[] Overlapping){
			if (Overlapping.Length >= 1) { // check if pieces are found
				// find highest stacking point
				float StackY = Overlapping [0].Top.y;

				for (int i = 1; i < Overlapping.Length; i++) {
					if (Overlapping [i].Top.y > StackY) {
						StackY = Overlapping [i].Top.y;
					}
				}

				if (StackY > Position.y) {
					PlaceAtPos (new Vector3(Position.x, StackY, Position.z),Vector3.zero,false); // place at position. Dont update last grid position. It will override the actual last grid position to the free from one
					return true;
				}
				return false;
			}
			return false;
		}


		public Vector3 CalculateRestrictedPosition(Vector3 Pos,SnapTypes Snap,Transform Trans=null){
			Snap = RestrictSnapType (Snap); // restrict snap type
			Vector3 FinalPos = Pos;
			switch (Snap) {
			case SnapTypes.Cross:
				FinalPos = (Trans)?GetSnapPosition(Pos,Rotation):GetSnapPosition(Pos); // snap to center of grid points
				break;
			case SnapTypes.Center:
				Vector3 GridOffset = Vector3.zero;
				GridOffset += ((Trans)?Trans.forward: Vector3.forward)* (RestrictedScale / 2);
				GridOffset += ((Trans)?Trans.right:Vector3.right)* (RestrictedScale / 2);
				FinalPos = (Trans)?GetSnapPosition(Pos,Rotation,GridOffset):GetSnapPosition(Pos,GridOffset); // snap to center of grid points
				break;
			case SnapTypes.Edge:
				Vector3 Forward = (Trans) ? Trans.forward : Vector3.forward; // get forward
				Vector3 Right = (Trans) ? Trans.right : Vector3.right; // get right
				Vector3 SnapOffset = GetEdgeSnapOffset (Forward,Right); // get edge snap offset
				Vector3 GridPosition = (Trans) ? GetSnapPosition (FinalPos, Rotation, SnapOffset) : GetSnapPosition (FinalPos, Quaternion.identity, SnapOffset); // get world or local snap position

				// calculate front or back side of edge
				Vector3 MouseWorldPos = Camera.main.ScreenToWorldPoint (new Vector3 (Input.mousePosition.x, Input.mousePosition.y, Camera.main.WorldToScreenPoint (FinalPos).z)); // world position of the mouse
				MouseWorldPos.y = 0; // set mouse world pos to 0
				float Side = (Vector3.Dot (transform.forward, (MouseWorldPos - GridPosition)) <= 0) ? -1 : 1; // check which edge to take
				SnapOffset = (transform.forward * Side) * (MinScale / 2); // offset position to be placed on edge
				FinalPos = GridPosition + SnapOffset; // override pos to be the new gridposition
				break;
			default:	
				// free form is default
				break;
			}
			return FinalPos;
		}
		// Grid snap positions
		private Vector3 GetSnapPosition(Vector3 Position,Quaternion Rotation,Vector3 Offset = default(Vector3)){
			Vector3 GridPosition = GlobalCalulations.RoundXandZLocal (RestrictedScale,Position,Rotation * Vector3.forward,GridCenter,Offset); // calculate offsetted grid point
			return GridPosition;
		}
		private Vector3 GetSnapPosition (Vector3 Position,Vector3 Offset = default(Vector3)){
			Vector3 GridPosition = GlobalCalulations.RoundXandZFloat (RestrictedScale, Position,Offset);
			return GridPosition;
		}
		private Vector3 GetEdgeSnapOffset(Vector3 LocalForward,Vector3 LocalRight){
			return (((Mathf.Round (Vector3.Dot (transform.forward, LocalForward)) == 0) ? LocalForward : LocalRight) * (RealScale * 0.5f)); // offset position to be placed on edge
		}

		private void AlignToSurface(Vector3 Normal){
			NoneOffsettedRotation = Quaternion.LookRotation (Normal).Multiply(Quaternion.Inverse(Quaternion.LookRotation (UpAxis)));
		}
		private void UpdateLastGridPosition(Vector3 PlacedPosition,Vector3 TargetPosition,Quaternion Rotation,SnapTypes TargetType){
			if ((SnapType == SnapTypes.Edge || SnapType == SnapTypes.Center) && TargetType == SnapTypes.Edge) {
				// non offsetted grid position
				LastGridPosition = GetSnapPosition(TargetPosition,Rotation);
			} else if(SnapType == SnapTypes.Edge && TargetType == SnapTypes.FreeForm || SnapType == SnapTypes.FreeForm && TargetType == SnapTypes.Edge) {
				// Place position - GridSnapoffset and default mouse at forward offset
				Vector3 GridPos = PlacedPosition;
				GridPos -= GetEdgeSnapOffset (Rotation * Vector3.forward, Rotation * Vector3.right) + ((Rotation * Vector3.forward) * MinScale / 2);
				LastGridPosition = GridPos;
			} else if(TargetType == SnapTypes.Center){
				LastGridPosition = GetSnapPosition(TargetPosition,Rotation);
			} else {
				// Last grid position is simply the placed location
				LastGridPosition = PlacedPosition;
			}
		}
		private void UpdateRotation(){
			transform.localRotation = Quaternion.Euler (GlobalCalulations.RoundVector(RotationStepRestriction,_NoneOffsettedRotation.Multiply(RotationOffset).eulerAngles)); // set rotation
		}

		private SnapTypes RestrictSnapType(SnapTypes Type){
			switch (PositionRestriction) {
			case PositionRestrictions.GridOnly:
				Type = SnapTypes.Center;
				break;
			}

			return Type;
		}

		// overlapping checks
		public ModularPlacableObject[] OverlappingObjects(LayerMask Layer,float? Range,bool CheckDefines){
			List<ModularPlacableObject> Overlapping = new List<ModularPlacableObject> ();
			Collider[] OverlappingColliders = Physics.OverlapSphere(Position,Scale);
			foreach (Collider col in OverlappingColliders) {
				ModularPlacableObject TempObj = (ModularPlacableObject)col.gameObject.GetComponent (typeof(ModularPlacableObject));
				if(TempObj != null && TempObj != this && TempObj.gameObject.layer == Layer.ToLayer()){
					// has found a object
					ModularPlacableObject Add = this.StackTest (TempObj, Range, CheckDefines);
					if (Add != null) {
						Overlapping.Add (Add);
					}
				}
			}

			return Overlapping.ToArray ();
		}
		public void RemoveFromGrid(){
			var DisabledColliders = SetCollidersActive (false); // disable colliders
			OnUpdateGrid (); // update grid
			SetCollidersActive (DisabledColliders,true); // enable colliders
		}
		public void SetCollidersActive(Collider[] Colliders,bool Active){
			for (int i = 0; i < Colliders.Length; i++) {
				Colliders [i].enabled = Active;
			}
		}
		public Collider[] SetCollidersActive(bool Active,bool IncludeInactive = false){
			Collider[] ScopedColliders = this.GetComponentsInChildren<Collider> (IncludeInactive);
			SetCollidersActive (ScopedColliders,Active);
			return ScopedColliders;
		}
		#endregion

		#region Private functions
		protected void RefillColorLinks(){
			_ColorLinks.Clear (); // clear color links

			Utils.RenderSet.Set[] Sets = RenderSet.Sets.ToArray();
			// add basic color links
			for (int i = 0; i < Sets.Length; i++) {
				Renderer Renderer = Sets [i].Renderer;
				if (Renderer.sharedMaterial != null && Renderer.sharedMaterial.HasProperty ("_ColorMaskCount")) {
					int count = Renderer.sharedMaterial.GetInt ("_ColorMaskCount");

					if (!ColorsFromMaterial) { // get colors from material
						MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
						Renderer.GetPropertyBlock (PropertyBlock);
						count = (int)PropertyBlock.GetFloat ("_ColorMaskCount");
					}

					// add color links with a max of 4
					if (count > 0) {
						_ColorLinks.Add (new ColorLink (RenderSet.GetRendererColor (i, "_MaskColor1"), RenderSet, i,ColorsFromMaterial, 0)); // add color link 1
					}
					if (count > 1) {
						_ColorLinks.Add (new ColorLink (RenderSet.GetRendererColor (i, "_MaskColor2"), RenderSet, i,ColorsFromMaterial, 1)); // add color link 2
					}
					if (count > 2) {
						_ColorLinks.Add (new ColorLink (RenderSet.GetRendererColor (i, "_MaskColor3"), RenderSet, i,ColorsFromMaterial, 2)); // add color link 3
					}
				}
			}
			_ColorLinks = _ColorLinks.CollapseLinks (); // collapse color links
		}
		protected virtual void RefillTextureLinks(){
			_TextureLinks.Clear ();
		}
		#endregion

		#region Protected functions
		protected void UpdateGrid(Vector3 Center, Vector3 Extends){
			if (CutGrid && Management.GameManager.I.Dinosaurs.Grid != null) {
				Management.GameManager.I.Dinosaurs.Grid.UpdateNodes (new Vector2(Center.x,Center.z), new Vector2(Extends.x,Extends.z),true);
				Management.GameManager.I.Modular.UpdateRegions ();
			}
		}
		#endregion

		#region IUndoable implementation
		public void OnUndo ()
		{
			RemoveFromGrid (); // remove self from grid
			OnModularUndo ();
		}
		public void OnRedo ()
		{
			OnUpdateGrid ();
			OnModularRedo ();
		}
		public void OnUndoDestoy ()
		{
			
		}
		#endregion

		public string Title {
			get {
				return gameObject.name;
			}
		}

		#region IDynamicObstacle implementation

		public bool IsObstacle {
			get {
				return CutGrid;
			}
		}

		#endregion

		#region Getters Setters
		public Utils.RenderSet RenderSet{
			get{
				if (_RenderSet == null) {
					this._RenderSet = GetComponent<Utils.RenderSet> (); // get renderset if is null
				}
				return _RenderSet;
			}
		}
		public Vector3 LastGridPosition{
			get{
				return _LastGridPosition;
			}
			set{
				_LastGridPosition = value; // set last grid position
			}
		}
		public Vector3 LastPosition{
			get{
				return _LastPosition;
			}
			set{
				_GridPosChanged = (_LastPosition != value) ? true : false;
				_LastPosition = value;
			}
		}
		public float LastYStack{
			get{
				return _LastStackY;
			}
			set{
				_LastStackY = value;
			}
		}
		public Quaternion Rotation{
			get{
				return transform.localRotation;
			}
		}
		public Quaternion RotationOffset{
			get{
				return _RotationOffset;
			}
			set {
				_RotationOffset = value;
				UpdateRotation (); // update rotation
			}
		}
		public Quaternion NoneOffsettedRotation{
			get{
				return _NoneOffsettedRotation;
			}
			set{
				_NoneOffsettedRotation = value;
				UpdateRotation (); // update rotation
			}
		}
		public bool GridPosChanged{
			get{
				return _GridPosChanged;
			}
		}
		public Vector3 Top{
			get{
				return Bounds.LocalTop;
			}
		}
		public Vector3 Bottom{
			get{
				return Bounds.LocalBottom;
			}
		}
		public void PlaceOnTop(Vector3 Pos){
			Position = Pos;
		}
		public float RestrictedScale{
			get{
				if (PositionRestriction != PositionRestrictions.None) {
					return PositionGridRestriction;
				} else {
					return Scale;
				}
			}
		}
		#endregion
		
		#region Virtuals
		/// <summary>
		/// Does this piece define the boundarys of the modular set it belongs to?
		/// </summary>
		/// <returns>Defines wheter this piece should influence the boundarys of the modular set.</returns>
		public virtual float Scale{
			get{
				return Bounds.Scale;
			}
		}
		public virtual ColorLink[] Colors{
			get{
				return _ColorLinks.ToArray();
			}
		}
		public virtual List<TextureLink> Textures{
			get{
				return _TextureLinks;
			}
			set{
				_TextureLinks = value;
			}
		}
		public virtual float MinScale{
			get{
				return Bounds.MinScale;
			}
		}
		public virtual bool DefinesBoundarys{
			get{
				return false;
			}
		}
		public virtual float RealScale{
			get{
				return Bounds.MaxExtend;
			}
		}

		/// <summary>
		/// Gets the type of the snap. (Default is: default)
		/// </summary>
		/// <value>The type of the snap.</value>
		public virtual SnapTypes SnapType{
			get{
				return DefaultSnapType;
			}
		}
		public virtual SnapTypes DefaultSnapType{
			get{
				return SnapTypes.FreeForm;
			}
		}
		public virtual Vector3 GridCenter{		
			get{
				return Vector3.zero;
			}
		}
		public virtual Vector3 AxisBoundsOffset{
			get{
				return Vector3.zero;
			}
		}
		public virtual Transform LocalSpace{
			get{
				return transform;
			}
		}
		public virtual Vector3 LocalPosition{
			get{
				return transform.localPosition;
			}
			set{
				transform.localPosition = value;
			}
		}
		public virtual Vector3 Position{
			get{
				return transform.position;
			}
			set{
				transform.position = value;
			}
		}
		public virtual AxisBounds Bounds{
			get{
				ErrorCheckBounds ();
				return _Bounds;
			}
			set{
				_Bounds = value;
			}
		}
		public virtual bool Editable{
			get{
				return false;
			}
		}
		public virtual Vector3 UpAxis{
			get{
				return Vector3.up;
			}
		}

		// restrictions
		public virtual SpaceRestrictions SpaceRestriction{
			get{
				return SpaceRestrictions.None;
			}
			set{
				// default empty set
			}
		}
		public virtual float RotationStepRestriction{
			get{
				return 0;
			}
			set{
				// default empty set
			}
		}
		public virtual RotationRestrictions RotationRestriction{
			get{
				return RotationRestrictions.XYZ;
			}
			set{
				// default empty set
			}
		}
		public virtual PositionRestrictions PositionRestriction{
			get{
				return PositionRestrictions.None;
			}
			set{
				// default empty set
			}
		}
		public virtual LayerMask SelectionLayerMask{
			get{
				return this.gameObject.layer.ToLayerMask ();
			}
		}
		public virtual float PositionGridRestriction{
			get{
				return Scale;
			}
			set{

			}
		}
		public virtual bool Scalable{
			get{
				return true;
			}
			set{
				// default empty set
			}
		}
		public virtual bool CutGrid{
			get{
				return false;
			}
		}
		protected virtual bool ColorsFromMaterial{
			get{
				return true;
			}
		}

		/// <summary>
		/// Get the maximal extend of this modular placable object and the group it belongs to
		/// </summary>
		/// <value>The max extend bounds.</value>
		public virtual AxisBounds MaxExtendBounds{
			get{
				return Bounds;
			}
		}

		// public voids
		public virtual void UpdateTransform(){}
		public virtual void OnPositionChanged(){}
		public virtual void OnPlaced(){
			OnUpdateGrid ();
		}
		public virtual void OnUpdateGrid(){
			UpdateGrid (Bounds.WorldCenter,Bounds.WorldExtends);
		}
		public virtual void OnDeplaced(){}

		// protected
		protected virtual void OnAwake(){}
		protected virtual void OnStart(){}
		protected virtual void OnModularUndo(){}
		protected virtual void OnModularRedo(){}

		/// <summary>
		/// Destory this modular placable instance.
		/// </summary>
		public virtual void Destroy(){
			GameObject.Destroy (this.gameObject);
		}
		public virtual void DestoyUndo(){
			this.gameObject.DestroyUndoable ();
		}
		public virtual void AutoFillRenderSet(){
			RenderSet.AutoFindAll ();
		}
		public virtual ModularPlacableObject Duplicate(){
			ModularPlacableObject DuplicateModularPlacable = GameObject.Instantiate (this.gameObject, this.transform.position, this.transform.rotation).GetComponent<ModularPlacableObject>();
			return DuplicateModularPlacable;
		}
		public virtual bool CanDuplicate(out string Message){
			Message = "";
			return true;
		}
		#endregion
		
		#region Error checks
		private void ErrorCheckBounds(){
			if (_Bounds.center == Vector3.zero && _Bounds.size == Vector3.zero) {
				//Debug.LogError ("Bounds is not set");
			} 
		}
		#endregion

		#region Structs / inner classes
		// Color
		public class ColorLink{
			// public vars
			public Color _Color;
			public List<RendererTuple> Tuples = new List<RendererTuple>();

			// private vars
			private int MaskIndex = 0;
			private bool UseColors;

			public ColorLink(Color col,Utils.RenderSet Set, int SetID,bool UseColors, int MaskIndex = 0){
				this._Color = col;
				this.MaskIndex = MaskIndex;
				this.UseColors = UseColors;
				AddRenderTuple(Set,SetID);
			}

			#region public input
			public void AddRenderTuple(Utils.RenderSet Set, int SetID){
				Tuples.Add (new RendererTuple(Set, SetID));
			}
			public void AddColorLink(ColorLink Link){
				Tuples.AddRange (Link.Tuples);
			}
			public void ClearRenderTuples(){
				Tuples.Clear ();
			}
			public void UpdateColors(){
				for (int i = 0; i < Tuples.Count; i++) {
					if (MaskIndex == 0) {
						Tuples [i].RenderSet.SetRendererColors (Tuples [i].SetID, "_MaskColor1", Color,UseColors);
					} else if (MaskIndex == 1) {
						Tuples [i].RenderSet.SetRendererColors (Tuples [i].SetID, "_MaskColor2", Color,UseColors);
					} else if (MaskIndex == 2) {
						Tuples [i].RenderSet.SetRendererColors (Tuples [i].SetID, "_MaskColor3", Color,UseColors);
					}
				}
			}
			#endregion

			#region Get / Set
			public Color Color{
				get{
					return _Color;
				}
				set{
					_Color = value;
					UpdateColors (); // update colors
				}
			}
			#endregion
		}
		public class ColorGroup{
			#region Private variables
			private Color _Col;
			private List<ColorLink> Links = new List<ColorLink>();
			#endregion

			public ColorGroup(ColorLink link){
				this._Col = link.Color;
				this.Links.Add(link);
			}

			#region Public inputs
			public void AddLink(ColorLink Link){
				Links.Add (Link);
			}
			#endregion

 			#region private void
			private void UpdateColors(){
				for (int i = 0; i < Links.Count; i++) {
					Links [i].Color = Color;
				}
			}
			#endregion

			#region Get / Setters
			public Color Color{
				get{
					return _Col;
				}
				set{
					this._Col = value;
					UpdateColors ();
				}
			}
			#endregion
		}

		public class TextureLink{
			// public vars
			public ModularTextureArray _Array;
			public ModularTextureData _Texture;
			public List<RendererTuple> Tuples = new List<RendererTuple>();
			public ModularPlacableObject ModularPlacable;

			public TextureLink(ModularTextureData Texture,ModularTextureArray Array,ModularPlacableObject ModularPlacable,Utils.RenderSet Set, int SetID){
				this._Texture = Texture;
				this._Array = Array;
				this.ModularPlacable = ModularPlacable;
				AddRenderTuple(Set,SetID);
			}

			#region public input
			public void AddRenderTuple(Utils.RenderSet Set, int SetID){
				Tuples.Add (new RendererTuple(Set, SetID));
			}
			public void AddRenderTuple(List<RendererTuple> RenderTuples){
				Tuples.AddRange (RenderTuples);
			}
			public void AddTextureLink(TextureLink Link){
				AddRenderTuple (Link.Tuples);
			}
			public void ClearRenderTuples(){
				Tuples.Clear ();
			}
			public void UpdateTextures(ModularTextureData Last){
				MonoBehaviour.print ("Harro");
				for (int i = 0; i < Tuples.Count; i++) {
					ApplyTextureData (Tuples[i].RenderSet.Sets[Tuples[i].SetID].Renderer,Texture);
				}
				ModularPlacable.RefillColorLinks ();
			}
			public void ApplyTextureData(Renderer Renderer,ModularTextureData Texture){
				MaterialPropertyBlock Block = new MaterialPropertyBlock ();

				// set texture properties
				if (Texture.Albedo != null) {
					Block.SetTexture ("_MainTex", Texture.Albedo);
				}
				if (Texture.MGA != null) {
					Block.SetTexture ("_Metallic", Texture.MGA);
				}
				if (Texture.NormalMap != null) {
					Block.SetTexture ("_BumpMap", Texture.NormalMap);
				}
				if (Texture.Emissive != null) {
					Block.SetTexture ("_EmissionMap", Texture.Emissive);
				}
				if (Texture.ColorMask != null) {
					Block.SetTexture ("_ColorMask", Texture.ColorMask);
				} else {
					Texture2D BlankTexture = new Texture2D (1, 1);
					BlankTexture.SetPixel (0,0,Color.red);
					BlankTexture.Apply ();
					Block.SetTexture ("_ColorMask", BlankTexture);
				}

				// Set values
				Block.SetFloat ("_Glossiness", Texture.Smoothness);
				Block.SetFloat ("_AdditionalGlossiness", Texture.AdditionalSmoothness);
				Block.SetFloat ("_AmbientStrength", Texture.AmbientOcclusion);
				Block.SetFloat ("_EmissiveBrightness", Texture.EmissiveBrightness);
				Block.SetVector ("_EmissionColor", Texture.EmissiveColor);

				// Set mask values
				Block.SetFloat ("_ColorMaskCount", Texture._ColorMaskCount);

				for(int i = 0; i < Texture._ColorMaskCount; i++){
					Block.SetVector ("_MaskColor"+(i+1),Texture._MaskColors[i]);
					Block.SetVector ("_CounterColor"+(i+1),Texture._AverageColors[i]);
				}

				Renderer.SetPropertyBlock (Block);
			}
			#endregion

			#region Get / Set
			public ModularTextureData Texture{
				get{
					return _Texture;
				}
				set{
					ModularTextureData Last = _Texture;
					_Texture = value;
					UpdateTextures (Last); // update colors
				}
			}
			#endregion
		}
		public struct RendererTuple{
			public Utils.RenderSet RenderSet;
			public int SetID;

			public RendererTuple(Utils.RenderSet Set,int SetID){
				this.SetID = SetID;
				this.RenderSet = Set;
			}
		}
		public class TextureGroup{
			#region Private variables
			private ModularTextureArray _TextureArray;
			private ModularTextureData _Texture;
			private List<TextureLink> Links = new List<TextureLink>();
			#endregion

			public TextureGroup(TextureLink link,ModularTextureArray TextureArray){
				this._Texture = link._Texture;
				this._TextureArray = TextureArray;
				this.Links.Add(link);
			}

			#region Public inputs
			public void AddLink(TextureLink Link){
				Links.Add (Link);
			}
			#endregion

			#region private void
			private void UpdateTextures(){
				for (int i = 0; i < Links.Count; i++) {
					Links [i].Texture = Texture;
				}
			}
			#endregion

			#region Get / Setters
			public ModularTextureArray TextureArray{
				get{
					return _TextureArray;
				}
			}
			public ModularTextureData Texture{
				get{
					return _Texture;
				}
				set{
					this._Texture = value;
					UpdateTextures ();
				}
			}
			#endregion
		}
		#endregion
	}
}