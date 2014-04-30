﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CharacterState {
	Idle = 0,
	Walking = 1,
	Trotting = 2,
	Running = 3,
	Jumping = 4,
	WallRunning = 5,
	PullingUp=6,
	DoubleJump= 7
}
public enum WallState{
	WallL,
	WallR,
	WallF
}

public class Pawn : DamagebleObject {

	public const int SYNC_MULTUPLIER = 5;

	public LayerMask groundLayers = -1;
	public LayerMask wallRunLayers = -1;
	public LayerMask climbLayers = 1 << 9; // Layer 9

	public bool isActive =true;

	public BaseWeapon CurWeapon;

	public Transform weaponSlot;

	public Transform myTransform;

	private Vector3 correctPlayerPos = Vector3.zero; //We lerp towards this

	private Quaternion correctPlayerRot = Quaternion.identity; //We lerp towards this

	public Vector3 weaponOffset;

	public Vector3 weaponRotatorOffset;

	public bool isDead=false;

	public string publicName;

	private Vector3 aimRotation;
	//rotation for moment when rotation of camera and pawn can be different e.t.c wall run	
	private Vector3 forwardRotation;

	public AnimationManager animator;

	private CharacterState _characterState;

	private CharacterState characterState
	{
		
		get {
			return _characterState;
		}
		
		
		set {
			if(_characterState!=value){
				photonView.RPC("SendCharacterState",PhotonTargets.Others,value,wallState);
				
			}
			_characterState = value;
			
		}
		
	}

	private WallState wallState;

	private CharacterState nextState;

	private Vector3 nextMovement;

	public ThirdPersonCamera cameraController;

	public float pitchAngle;

	public bool isAi;

	public Pawn enemy;

	private Rigidbody _rb;

	private float distToGround;

	private CapsuleCollider capsule;

	public bool canWallRun;

	public bool canPullUp;

	public float wallRunSpeed;

	public float groundRunSpeed;

	public float groundTrotSpeed;

	public float groundWalkSpeed;

	public float jumpHeight;
	
	public float climbSpeed;

	public float climbCheckRadius = 0.1f;

	public float climbCheckDistance = 0.5f;

	public float heightOffsetToEdge = 2.0f;

	public float PullUpStartTimer= 0.0f;
	
	public float PullUpTime=2.0f;
	
	private float v;

	public Transform curLookTarget= null;

	public Player player=null;

	public int team;

	private List<Pawn> seenPawns=new List<Pawn>();

	public float seenDistance;

	private Collider myCollider;

	private bool _isGrounded;

	private bool netIsGround;

	public bool isGrounded
	{
		
		get {
			return _isGrounded;
		}

		
		set {
			if(_isGrounded!=value&& value){
				SendMessage ("DidLand", SendMessageOptions.DontRequireReceiver);

			}
			_isGrounded = value;

		}
		
	}
	private float lastTimeOnWall;

	private float lastJumpTime;

	private Vector3 floorNormal;

	private ContactPoint[] contacts;

	public Vector3 centerOffset;

	public Vector3 headOffset;

	public static float gravity = 20.0f;

	public InventoryManager ivnMan;

	public bool isAiming=false;

	public float aimModCoef = -10.0f;

	public bool isLookingAt = true;
	
	public class BasePawnStatistic{
		//Shoot Counter
		public int shootCnt=0;
	
	};
	
	public BasePawnStatistic statistic = new BasePawnStatistic();
	// Use this for initialization
	void Start () {
		maxHealth = health;
		 photonView = GetComponent<PhotonView>();
		if (!photonView.isMine) {
						Destroy (GetComponent<ThirdPersonController> ());
						Destroy (GetComponent<ThirdPersonCamera> ());
						Destroy (GetComponent<MouseLook> ());
						GetComponent<Rigidbody> ().isKinematic = true;
		} else {
			cameraController=GetComponent<ThirdPersonCamera> ();
			isAi = cameraController==null;
		}
		ivnMan =GetComponent<InventoryManager> ();
		myTransform = transform;
		correctPlayerPos = transform.position;
		myCollider = collider;
		_rb  = GetComponent<Rigidbody>();
		capsule = GetComponent<CapsuleCollider> ();
		centerOffset = capsule.bounds.center - myTransform.position;
		headOffset = centerOffset;
		headOffset.y = capsule.bounds.max.y - myTransform.position.y;

		distToGround = capsule.height/2-capsule.center.y;
		//Debug.Log (distToGround);
	}
	
	public override void Damage(BaseDamage damage,GameObject killer){
		Pawn killerPawn =killer.GetComponent<Pawn> ();
		if (killerPawn != null && killerPawn.team == team &&! PlayerManager.instance.frendlyFire) {
			return;
		}
		if (killerPawn != null){
			Player killerPlayer =  killerPawn.player;
			if(killerPlayer!=null){
				killerPlayer.DamagePawn(damage.Damage,myTransform.position +new Vector3 (Random.Range (-1 , 1),Random.Range (-1 , 1),Random.Range (-1 , 1)));
			}
		}
		if (!PhotonNetwork.isMasterClient){
			return;
		}
	
		
		
		
		//Debug.Log ("DAMAGE");
		base.Damage(damage,killer);
	}

	public void Heal(float damage,GameObject Healler){
		health += damage;
		if (maxHealth < health) {
			health=maxHealth;		
		}

	}

	public override void KillIt(GameObject killer){
		if (isDead) {
			return;		
		}
		isDead = true;
		StartCoroutine (CoroutineRequestKillMe ());
		Pawn killerPawn =killer.GetComponent<Pawn> ();
		Player killerPlayer = null;
		if (killerPawn != null) {
			killerPlayer = killerPawn.player;
			if(killerPlayer!=null){
				killerPlayer.PawnKill(player,myTransform.position);
			}
		}
		//Debug.Log ("KILLL IT" + player);
		if (player != null) {
			if(player.inBot){
				player.RobotDead(killerPlayer);
			}else{
				player.PawnDead(killerPlayer);
			}
		}

		if (CurWeapon != null) {
			CurWeapon.	RequestKillMe();
		}


		
	}


	// Update is called once per frame
	void Update () {
		//Debug.Log (photonView.isSceneView);
		if (!isActive) {
			return;		
		}

		if (photonView.isMine) {
						if (isAi) {
								Quaternion aimRotation = Quaternion.LookRotation (enemy.myTransform.position - myTransform.position);
								pitchAngle = aimRotation.eulerAngles.x;
						} else {
								pitchAngle = -cameraController.yAngle;

						}
			Pawn[] allPawn =PlayerManager.instance.FindAllPawn();
			seenPawns.Clear();

			for(int i=0;i<allPawn.Length;i++){
				if(allPawn[i]==this){
					continue;
				}
				Vector3 distance =(allPawn[i].myTransform.position-myTransform.position); 
				if(distance.sqrMagnitude<seenDistance){
					RaycastHit hitInfo;
					Vector3 normalDist = distance.normalized;
					Vector3 startpoint = myTransform.position +normalDist*capsule.radius;
					//Debug.DrawLine(startpoint,normalDist*100+startpoint);
					if (allPawn[i].team!=team&&Physics.Raycast(startpoint,normalDist,out hitInfo)) {
						//Debug.Log(hitInfo.collider);
				
						if(allPawn[i].myCollider!=hitInfo.collider){
							continue;
						}
					}
					seenPawns.Add(allPawn[i]);
				}

			}

			if(CurWeapon!=null){
				//if(aimRotation.sqrMagnitude==0){
				getAimRotation(CurWeapon.weaponRange);
				/*}else{
					aimRotation = Vector3.Lerp(aimRotation,getAimRotation(CurWeapon.weaponRange), Time.deltaTime*10);
				}*/
				Vector3 eurler = Quaternion.LookRotation(aimRotation-myTransform.position).eulerAngles;
				eurler.z =0;
				eurler.x =0;
				if(characterState == CharacterState.WallRunning||characterState ==CharacterState.PullingUp){
					if(forwardRotation.sqrMagnitude>0){
						myTransform.rotation= Quaternion.LookRotation(forwardRotation);
					}
				}else{
					myTransform.rotation= Quaternion.Euler(eurler);
				}
				//CurWeapon.curTransform.rotation =  Quaternion.LookRotation(aimRotation-CurWeapon.curTransform.position);
				/*Quaternion diff = Quaternion.identity;
				Vector3 target = (aimRotation-CurWeapon.transform.position).normalized;
				if(!CurWeapon.IsReloading()){
					diff= Quaternion.FromToRotation(CurWeapon.transform.forward,target);
				}

				Debug.DrawLine(CurWeapon.transform.position,aimRotation);
				Vector3 aimRotationWeapon = diff*target*CurWeapon.weaponRange+CurWeapon.transform.position; 
				Debug.DrawLine(CurWeapon.transform.position,aimRotationWeapon);*/

			}else{
				//if(aimRotation.sqrMagnitude==0){
					getAimRotation(50);
				/*}else{
					aimRotation = Vector3.Lerp(aimRotation,getAimRotation(50), Time.deltaTime*10);
				}*/
				Vector3 eurler = Quaternion.LookRotation(aimRotation-myTransform.position).eulerAngles;
				eurler.z =0;
				eurler.x =0;
				if(characterState == CharacterState.WallRunning||characterState ==CharacterState.PullingUp){
					if(forwardRotation.sqrMagnitude>0){
						myTransform.rotation= Quaternion.LookRotation(forwardRotation);
					}
				}else{
					myTransform.rotation= Quaternion.Euler(eurler);
				}
				

				
			}
			//TODO: TEMP SOLUTION BEFORE NORMAL BONE ORIENTATION
			
			//animator.SetFloat("Pitch",pitchAngle);

		} else {

			myTransform.position = Vector3.Lerp(myTransform.position, correctPlayerPos, Time.deltaTime *SYNC_MULTUPLIER);
			myTransform.rotation = Quaternion.Lerp(myTransform.rotation, correctPlayerRot, Time.deltaTime * SYNC_MULTUPLIER);

		}
//		Debug.Log (characterState);
		float strafe = 0;
		//Debug.Log (strafe);	
		float speed =0 ;
		//Debug.Log (speed);
		if (animator != null && animator.gameObject.activeSelf) {
			if (photonView.isMine) {


				 strafe = CalculateStarfe();
				//Debug.Log (strafe);	
				 speed =CalculateSpeed();
				if (characterState == CharacterState.Idle) {
					animator.ApllyMotion (0.0f, speed, strafe);
								} else {
										if (characterState == CharacterState.Running) {
												animator.ApllyMotion (2.0f, speed, strafe);
										} else if (characterState == CharacterState.Trotting) {
												animator.ApllyMotion (1.0f, speed, strafe);	
										} else if (characterState == CharacterState.Walking) {
												animator.ApllyMotion (1.0f, speed, strafe);	
										} else if (characterState == CharacterState.WallRunning) {
												//Debug.Log ("INSWITCH");
												switch (wallState) {
												case WallState.WallF:
														animator.WallAnimation (false, false, true);
														break;
												case WallState.WallR:
														animator.WallAnimation (false, true, false);
														break;
												case WallState.WallL:
														animator.WallAnimation (true, false, false);
														break;
												}

										}
								}
							
				//
					}else{
				strafe = CalculateRepStarfe();
				//Debug.Log (strafe);	
				 speed =CalculateRepSpeed();
							switch(nextState){
								case CharacterState.Idle:
									if(characterState == CharacterState.Jumping){
										animator.ApllyJump(false);
									}
									animator.ApllyMotion (0.0f, speed, strafe);
									break;
								case CharacterState.Running:
									if(characterState == CharacterState.Jumping){
										animator.ApllyJump(false);
									}
									animator.ApllyMotion (2.0f, speed, strafe);
									break;
								case CharacterState.Trotting:
									if(characterState == CharacterState.Jumping){
										animator.ApllyJump(false);
									}
									animator.ApllyMotion (1.0f, speed, strafe);
									break;
								case CharacterState.Walking:
										if(characterState == CharacterState.Jumping){
											animator.ApllyJump(false);
										}
										animator.ApllyMotion (1.0f, speed, strafe);
									break;
								case CharacterState.WallRunning:
									switch (wallState) {
									case WallState.WallF:
										animator.WallAnimation (false, false, true);
									break;
									case WallState.WallR:
										animator.WallAnimation (false, true, false);
										break;
									case WallState.WallL:
										animator.WallAnimation (true, false, false);
										break;
									}
									break;
								case CharacterState.Jumping:
									animator.ApllyJump(true);
									if(characterState==CharacterState.WallRunning){					
										animator.WallAnimation(false,false,false);
									}
									break;
								case CharacterState.DoubleJump:
									animator.ApllyJump(true);
									if(characterState==CharacterState.WallRunning){					
										animator.WallAnimation(false,false,false);
									}
									break;
								case CharacterState.PullingUp:
									if(characterState!=CharacterState.PullingUp){
										StartCoroutine("PullUpEnd",PullUpTime);
										animator.StartPullingUp();
									}
								break;
							
					
								}
								characterState = nextState;
					}
			if (isLookingAt) {
				animator.animator.SetLookAtPosition (aimRotation);
				animator.animator.SetLookAtWeight (1, 0.5f, 0.7f, 0.0f, 0.5f);
			}
		}
		
	}
	[RPC]
	public void SendCharacterState(int nextrpcState,int nextwallState){
		wallState = (WallState)nextwallState;
		nextState =(CharacterState) nextrpcState;
	}
	//Weapon Section
	public void StartFire(){

		if (CurWeapon != null) {
			CurWeapon.StartFire ();
		}
	}
	public void StopFire(){
		if (CurWeapon != null) {
			CurWeapon.StopFire ();
		}
	}
	public void setWeapon(BaseWeapon newWeapon){
		CurWeapon = newWeapon;
		//Debug.Log (newWeapon);
		CurWeapon.AttachWeapon(weaponSlot,weaponOffset,Quaternion.Euler (weaponRotatorOffset),this);
	}
	public Vector3 getAimRotation(float weaponRange){
		
		if(photonView.isMine){
			if(isAi){
				aimRotation = enemy.myTransform.position;
			}else{
				if(cameraController.enabled ==false){
					aimRotation= myTransform.position +myTransform.forward*50;
					return aimRotation;
				}
				Camera maincam = Camera.main;
				Ray centerRay= maincam.ViewportPointToRay(new Vector3(.5f, 0.5f, 1f));
				RaycastHit hitInfo;
				Vector3 targetpoint = Vector3.zero;
				if (Physics.Raycast (centerRay,out hitInfo, weaponRange)&&hitInfo.collider!=collider) {
					targetpoint =hitInfo.point;
					curLookTarget= hitInfo.transform;
					//Debug.Log (curLookTarget);
				//	Debug.Log((targetpoint-myTransform.position).sqrMagnitude.ToString()+(cameraController.normalOffset.magnitude+5));
					if((targetpoint-myTransform.position).sqrMagnitude<cameraController.normalOffset.magnitude+5){
						targetpoint =maincam.transform.forward*weaponRange +maincam.ViewportToWorldPoint(new Vector3(.5f, 0.5f, 1f));
						animator.WeaponDown(true);
					}else{
						animator.WeaponDown(false);
					}
				}else{
					targetpoint =maincam.transform.forward*weaponRange +maincam.ViewportToWorldPoint(new Vector3(.5f, 0.5f, 1f));
				}
				aimRotation=targetpoint; 
				
			}
			
			return aimRotation;
		}else{
			return aimRotation;
		}
	}
	public Vector3 getCachedAimRotation(){
		return aimRotation;

	}

	public float AimingCoef(){
		if (isAiming) {
			return aimModCoef;		
		}
		return 0.0f;
	}

	public void ToggleAim(){
		isAiming = !isAiming;
		if (cameraController != null) {
			cameraController.ToggleAim();
		}
	}

	public int 	GetAmmoInBag (){
		return ivnMan.GetAmmo (CurWeapon.ammoType);

	}
	//END WEAPON SECTION
	void OnCollisionEnter(Collision collision) {
		//Debug.Log ("COLLISION ENTER PAWN " + this + collision);
	}
	void OnTriggerEnter	(Collider other) {
		//Debug.Log ("TRIGGER ENTER PAWN "+ this +  other);
	}

	//TODO: MOVE THAT to PAwn and turn on replication of aiming
	//TODO REPLICATION
	


		

	//Movement section

	float CalculateStarfe(){
		return Vector3.Dot (myTransform.right, _rb.velocity.normalized);
				
	
	}
	float CalculateSpeed(){
		float result =Vector3.Project (_rb.velocity,myTransform.forward).magnitude;
		if (result < groundWalkSpeed) {
			return 0.0f;		
		}
		if (result > groundWalkSpeed && result < groundTrotSpeed) {
			return 1.0f*Mathf.Sign(Vector3.Dot(_rb.velocity.normalized,myTransform.forward));	
		}
		if (result > groundTrotSpeed) {
			return 2.0f*Mathf.Sign(Vector3.Dot(_rb.velocity.normalized,myTransform.forward));	
		}
		return 0.0f;		
	}
	float CalculateRepStarfe(){
		Vector3 velocity =  correctPlayerPos-myTransform.position;
		return Vector3.Dot (myTransform.right, velocity.normalized);
				
	
	}
	float CalculateRepSpeed(){
		Vector3 velocity =  correctPlayerPos-myTransform.position;
		velocity = velocity/(Time.deltaTime * SYNC_MULTUPLIER);
		float result =Vector3.Project (velocity,myTransform.forward).magnitude;
		if (result < groundWalkSpeed) {
			return 0.0f;		
		}
		if (result > groundWalkSpeed && result < groundTrotSpeed) {
			return 1.0f*Mathf.Sign(Vector3.Dot(velocity.normalized,myTransform.forward));	
		}
		if (result > groundTrotSpeed) {
			return 2.0f*Mathf.Sign(Vector3.Dot(velocity,myTransform.forward));	
		}
		return 0.0f;		
	}
	public void Movement(Vector3 movement,CharacterState state){
		//Debug.Log (state);

		nextState = state;

		if (nextState != CharacterState.Jumping&&nextState != CharacterState.DoubleJump) {
						movement = (movement - Vector3.Project (movement, floorNormal)).normalized * movement.magnitude;
						//Debug.DrawRay (myTransform.position, movement.normalized);
						//Debug.DrawRay (myTransform.position, floorNormal);
						nextMovement = movement;
		} else {
			nextMovement = movement;
			}

	}

	bool WallRun (Vector3 movement,CharacterState state)
	{
		if (!canWallRun&&photonView.isMine) return false;

		//if (isGrounded) return false;
		if (lastTimeOnWall + 1.0f > Time.time) {
			return false;
		}
		
		if (_rb.velocity.sqrMagnitude < 0.2f ) {
			if(characterState == CharacterState.WallRunning){
					characterState = CharacterState.Jumping;
					lastTimeOnWall = Time.time;
			}
			return false;
		}
	
		//Debug.Log (movement);
		RaycastHit leftH,rightH,frontH;
		
		
		bool leftW = Physics.Raycast (myTransform.position ,
		                              myTransform.right * -1 ,out leftH, capsule.radius + 0.2f,wallRunLayers);
		bool rightW = Physics.Raycast (myTransform.position,
		                               myTransform.right ,out rightH, capsule.radius + 0.2f, wallRunLayers);
		bool frontW = Physics.Raycast (myTransform.position,
		                               myTransform.forward,out frontH, capsule.radius + 0.2f, wallRunLayers);

		/*Debug.DrawRay (myTransform.position ,
		               myTransform.right * -1 );
		
		Debug.DrawRay (myTransform.position,
		               myTransform.right );
		
		Debug.DrawRay (myTransform.position,
		               myTransform.forward);*/
	
	


		Vector3 tangVect = Vector3.zero;
		
		if(!animator.animator.IsInTransition(0) && !_rb.isKinematic)
		{
			if(leftW)
			{
				
				
				tangVect = Vector3.Cross(leftH.normal,Vector3.up);
				//tangVect = Vector3.Project(movement,tangVect).normalized;
				_rb.velocity = tangVect*wallRunSpeed + myTransform.up*wallRunSpeed/3;
				if(!(characterState == CharacterState.WallRunning))
				{
					wallState =WallState.WallL;
					characterState = CharacterState.WallRunning;
					//animator.SetBool("WallRunL", true);
					StartCoroutine( WallRunCoolDown(3f)); // Exclude if not needed
				}
				
				if(state == CharacterState.Jumping)
				{
					_rb.velocity = myTransform.up*movement.y  +leftH.normal*movement.y;
					StartCoroutine( WallJump(1f)); // Exclude if not needed
				}
			}
			
			else if(rightW)
			{
				
				tangVect = -Vector3.Cross(rightH.normal,Vector3.up);
				//tangVect = Vector3.Project(movement,tangVect).normalized;
				_rb.velocity = tangVect*wallRunSpeed + myTransform.up*wallRunSpeed/3;
				if(!(characterState == CharacterState.WallRunning))
				{
					wallState =WallState.WallR;
					characterState = CharacterState.WallRunning;
					StartCoroutine( WallRunCoolDown(3f)); // Exclude if not needed
				}
				
				if(state == CharacterState.Jumping)
				{
					_rb.velocity = myTransform.up*movement.y  +rightH.normal*movement.y;
					StartCoroutine( WallJump(1f)); // Exclude if not needed
				}
			}
			
			else if(frontW)
			{
				_rb.velocity = myTransform.up*wallRunSpeed/1.5f;
				tangVect = frontH.normal*-1;
				if(!(characterState == CharacterState.WallRunning))
				{
					wallState =WallState.WallF;
					characterState = CharacterState.WallRunning;
					StartCoroutine( WallRunCoolDown(3f)); // Exclude if not needed
				}
				
				if(state == CharacterState.Jumping)
				{
					_rb.velocity =( myTransform.up+myTransform.forward*-1).normalized*movement.y;
					StartCoroutine( WallJump(1f)); // Exclude if not needed
				}
			}else{
				if(characterState == CharacterState.WallRunning){
					characterState = CharacterState.Jumping;
					lastTimeOnWall = Time.time;
				}

			}

			forwardRotation  =  tangVect*5;
			//Debug.DrawLine(myTransform.position,forwardRotation);
			//animator.WallAnimation(leftW,rightW,frontW);
			

			return leftW||rightW||frontW;
		}
		return false;

	}
	// Wall run cool-down
	IEnumerator WallRunCoolDown (float sec)
	{
		canWallRun = true;
		yield return new WaitForSeconds (sec);
		canWallRun = false;
		characterState = CharacterState.Jumping;
		yield return new WaitForSeconds (sec);
		canWallRun = true;
	}
	// Wall run cool-down
	IEnumerator WallJump (float sec)
	{
		Jump ();
		//Debug.Log ("WALLJUMP");
		canWallRun = false;
		characterState = CharacterState.Jumping;
		yield return new WaitForSeconds (sec);
		canWallRun = true;
	}

	void OnCollisionStay(Collision collisionInfo) {
		if (lastJumpTime + 0.1f > Time.time) {
			return;		
		}
		if(characterState==CharacterState.WallRunning){
		   return;
		}
	    contacts = collisionInfo.contacts;
		if (contacts != null) {
			foreach (ContactPoint contact in contacts) {
				/*if(contact.otherCollider.CompareTag("decoration")){
					continue;
				}*/
				Vector3 Direction = contact.point - myTransform.position;
				//Debug.Log (this.ToString()+collisionInfo.collider+Vector3.Dot(Direction.normalized ,Vector3.down) );
				if (Vector3.Dot (Direction.normalized, Vector3.down) > 0.75) {
					isGrounded = true;
					floorNormal = 	contact.normal;
				}
			
				//Debug.DrawRay(contact.point, contact.normal, Color.white);

			}
			contacts= null;	
		}

	}
	public void FixedUpdate () {

		if (!isActive) {
			return;		
		}
		if (!photonView.isMine) {
			return;
		}

		if (isGrounded) {
			//Debug.Log ("Ground"+characterState);

			if (_rb.isKinematic) _rb.isKinematic= false;
			Vector3 velocity = rigidbody.velocity;
			Vector3 velocityChange = (nextMovement - velocity);
		
			rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);

			characterState = nextState;
			if(nextState==CharacterState.Jumping){
				Jump ();

			}

		} else {
			//Debug.Log ("Air"+characterState);
			v = nextMovement.normalized.magnitude;
			
			switch(nextState)
			{
			case CharacterState.DoubleJump:
				if(characterState!=CharacterState.WallRunning
				   &&characterState!=CharacterState.PullingUp){

					Vector3 velocity = rigidbody.velocity;
					Vector3 velocityChange = (nextMovement - velocity);
					//Debug.Log("DOUBLE JUMP");
					
					rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);

					characterState = nextState;
				}
				break;
			default:

				if(!WallRun (nextMovement,nextState)){

					animator.ApllyJump(true);						
					animator.WallAnimation(false,false,false);
					
					//Debug.Log ("My Name" +this +"  "+nextState+"  "+isGrounded);
				}else{
					SendMessage ("WallLand", SendMessageOptions.DontRequireReceiver);
				}
				if(PullUpCheck()){

					PullUp();
				}
				break;
			}
			
		}
		//Debug.Log(_rb.isKinematic);

		if (!_rb.isKinematic) {
			
			_rb.AddForce(new Vector3(0,-gravity * rigidbody.mass,0));
		}	
		netIsGround = isGrounded;
		if (photonView.isMine) {
						isGrounded = false;
		}
	}
	public bool IsGrounded ()
	{	

		return isGrounded;
	}
	public void Jump(){
		animator.ApllyJump(true);	
		lastJumpTime = Time.time;
		//photonView.RPC("JumpChange",PhotonTargets.OthersBuffered,true);
	}

	public void DidLand(){
		animator.ApllyJump(false);
		//Debug.Log ("LAND");
		lastTimeOnWall = -10.0f;
		//photonView.RPC("JumpChange",PhotonTargets.OthersBuffered,false);
	}

	bool PullUpCheck(){
		if (!canPullUp) {
			return false;
		}
		if (characterState == CharacterState.PullingUp) {
			return true;
		}

		RaycastHit frontH;
		bool frontW = Physics.Raycast (myTransform.position,
		                               myTransform.forward,out frontH, capsule.radius + 0.2f, wallRunLayers);
		bool middleAir = Physics.Raycast (myTransform.position+ myTransform.up/2,
		                                  myTransform.forward,out frontH, capsule.radius + 0.2f, wallRunLayers);
		if(frontW||middleAir){
			bool frontAir = Physics.Raycast (myTransform.position+ myTransform.up,
		                               myTransform.forward,out frontH, capsule.radius + 0.2f, wallRunLayers);
			forwardRotation= frontH.normal*-1;
	

			animator.SetLong(!middleAir);

			return !frontAir;
			
		}
		/*Debug.DrawRay (myTransform.position ,
		               myTransform.forward * -1 );
		Debug.DrawRay (myTransform.position+ myTransform.up ,
		               myTransform.forward * -1 );*/
		return false;
		//Deprecated system of collider pullup system
		/*Vector3 p1 = myTransform.position - (myTransform.up * -heightOffsetToEdge) + myTransform.forward;
		Vector3 p2 = myTransform.position - (myTransform.up * -heightOffsetToEdge);
		//Debug.DrawLine (p1, p2);
		RaycastHit hit;
		//Debug.DrawLine (p1-myTransform.up*climbCheckDistance, p2-myTransform.up*climbCheckDistance);
		// Hit nothing and not at edge -> Out
		return Physics.CapsuleCast (p1, p2, climbCheckRadius, -myTransform.up, out hit, climbCheckDistance, climbLayers);*/
		
	}
	// Wall run cool-down
	IEnumerator PullUpEnd (float sec)
	{
	
		yield return new WaitForSeconds (sec);
		_rb.isKinematic = false;
		animator.FinishPullingUp();
		characterState = CharacterState.Idle;
		isGrounded = true;
		SendMessage ("DidLand", SendMessageOptions.DontRequireReceiver);

	}

	void PullUp ()
	{
			//Debug.Log (characterState);
			if(	characterState != CharacterState.PullingUp){
				characterState = CharacterState.PullingUp;
				_rb.isKinematic = true;
				StartCoroutine("PullUpEnd",PullUpTime);
				animator.StartPullingUp();
				PullUpStartTimer = 0.0f;
			}
			PullUpStartTimer += Time.deltaTime;
			float nT = PullUpStartTimer/PullUpTime;

			if (nT <= 1.0f) {
						if (nT <= 0.4f) { // Step up
								myTransform.Translate (Vector3.up * Time.deltaTime * climbSpeed);
						} else { // Step forward
								if (nT <= 0.6f)
										myTransform.Translate (Vector3.forward * Time.deltaTime * climbSpeed);
								else if (nT >= 0.6f && _rb.isKinematic) // fall early
										_rb.isKinematic = false;
								if (!_rb.isKinematic)
										_rb.velocity = new Vector3 (0, _rb.velocity.y, 0);
						}
				}
						
				
	}

	public void StopMachine(){
		characterState = CharacterState.Idle;
		nextMovement = Vector3.zero;
	}
	//end Movement Section

	//NetworkSection
	public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
		if (stream.isWriting)
		{
			// We own this player: send the others our data
			stream.SendNext(transform.position);
			stream.SendNext(transform.rotation);
			stream.SendNext(aimRotation);
			//stream.SendNext(characterState);
			stream.SendNext(health);
			//stream.SendNext(wallState);
			stream.SendNext(netIsGround);
			//stream.SendNext(animator.GetJump());

		}
		else
		{
			// Network player, receive data
			Vector3 newPosition= (Vector3) stream.ReceiveNext();
			correctPlayerPos = newPosition;
			correctPlayerRot = (Quaternion) stream.ReceiveNext();
			this.aimRotation = (Vector3) stream.ReceiveNext();
			//nextState = (CharacterState) stream.ReceiveNext();
			//Debug.Log (characterState);
			health=(float) stream.ReceiveNext();
			//wallState = (WallState) stream.ReceiveNext();
			isGrounded =(bool) stream.ReceiveNext();
			//animator.ApllyJump((bool)stream.ReceiveNext());
			//Debug.Log (wallState);
		}
	}
	public void Activate(){
		if(cameraController!=null){
			_rb.isKinematic = false;
			isActive = true;
			_rb.detectCollisions = true;
			cameraController.enabled = true;
			cameraController.Reset();
			GetComponent<ThirdPersonController> ().enabled= true;
		}

		for (int i =0; i<myTransform.childCount; i++) {
			myTransform.GetChild(i).gameObject.SetActive(true);
		}
		photonView.RPC("RPCActivate",PhotonTargets.OthersBuffered);
	}
	[RPC]
	public void RPCActivate(){
		//Debug.Log ("RPCActivate");
		if(cameraController!=null){
			cameraController.enabled = true;
			isActive = true;
			GetComponent<ThirdPersonController> ().enabled= true;

		}
		for (int i =0; i<myTransform.childCount; i++) {
			myTransform.GetChild(i).gameObject.SetActive(true);
		}
	}
	public void DeActivate(){
		if(cameraController!=null){
			_rb.isKinematic = true;
			isActive = false;
			_rb.detectCollisions = false;
			cameraController.enabled = false;

			GetComponent<ThirdPersonController> ().enabled= false;
		}
		for (int i =0; i<myTransform.childCount; i++) {
			myTransform.GetChild(i).gameObject.SetActive(false);
		}
		photonView.RPC("RPCDeActivate",PhotonTargets.OthersBuffered);
		
	}
	[RPC]
	public void RPCDeActivate(){
		//Debug.Log ("RPCDeActivate");
		if(cameraController!=null){
			cameraController.enabled = false;
			isActive = false;
			GetComponent<ThirdPersonController> ().enabled= false;
		}
		for (int i =0; i<myTransform.childCount; i++) {
			myTransform.GetChild(i).gameObject.SetActive(false);
		}

	}
	//EndNetworkSection

	//Base Seenn Hear work

	public List<Pawn> getAllSeenPawn(){
		return seenPawns;
	}


	//end seen hear work
}
