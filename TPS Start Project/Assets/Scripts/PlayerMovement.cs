using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController characterController; // RigidBody가 아니기 때문에 물리 영향을 받지 않음
    private PlayerInput playerInput;
    private PlayerShooter playerShooter;
    private Animator animator;
    
    private Camera followCam;
    
    public float speed = 6f;
    public float jumpVelocity = 20f;
    [Range(0.01f, 1f)] public float airControlPercent;

    public float speedSmoothTime = 0.1f; // 부드럽게 움직일 수 있도록 하는 지연시간
    public float turnSmoothTime = 0.1f;
    
    private float speedSmoothVelocity; // 값의 연속적인 변화량을 기억하기 위한 변수, 직전까지의 값 (Damping, Smoothing 에서 사용)
    private float turnSmoothVelocity;
    
    private float currentVelocityY; // Y 방향의 속도를 저장하기 위한 변수, RigidBody가 아니기 때문에 물리 영향을 받지 않음
    
    // X,Z 방향에 대한 속도 (Y방향 제외)
    // magnitude 크기 
    public float currentSpeed => new Vector2(characterController.velocity.x, characterController.velocity.z).magnitude; 
    
    private void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        playerShooter = GetComponent<PlayerShooter>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        followCam = Camera.main;
    }

    // PlayerInput를 받는 메소드 
    // 물리, 회전주기에 대한 정확한 값을 받기 위해서 사용
    private void FixedUpdate()
    {
        if (currentSpeed > 0.2f || playerInput.fire || playerShooter.aimState == PlayerShooter.AimState.HipFire) 
            Rotate();

        Move(playerInput.moveInput);
        
        if (playerInput.jump) Jump();
    }

    // Animation 메소드 구현
    // 물리적인 정확한 값을 받게 되면 Animation의 오차가 발생할 수 있기 때문에
    private void Update()
    {
        UpdateAnimation(playerInput.moveInput);
    }

    public void Move(Vector2 moveInput)
    {
        var targetSpeed = speed * moveInput.magnitude;
        // 객체 이동벡터는  앞뒤쪽방향에 대한 이동 비율 + 좌우방향에 대한 이동 비율 (비율은 이미 normalized되어 있기 때문에 가능하다.)
        var moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;

        // 지연시간 계산
        // 지면에 있으면 기본 지연시간, 공중에 있으면 기본지연시간을 공중지연비율로 나눠서서 바닥보다는 더 지연되도록 값을 도출한다.
        var smoothTime = (characterController.isGrounded)? speedSmoothTime : speedSmoothTime / airControlPercent;

        // 보정된 속도 계산
        // 속도 변화량 (현재속도에서 타겟속도로 변화량)
        // speedSmoothVelocity : 직전까지의 값
        // Smoothing == Damping 
        targetSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, smoothTime);
        


        // 하지만 moveDirection의 크기가 1보다 클 수 있기 때문에 최종 normalized 처리를 한다. 
        moveDirection = Vector3.Normalize(moveDirection);
        // rigidbody가 아니기 때문에 y를 수동적으로 계산을 한다.  
        currentVelocityY += Time.deltaTime * Physics.gravity.y; // 가속도 = 시간당 * 속도 => 속도 = 시간간격 * 가속도 (중력가속도의 기본값: -9.8)

        var velocity = moveDirection * targetSpeed + transform.up * currentVelocityY;
        // velocity는 실제 속도가 아니라 거리이 때문에 시간을 곱해서 속도를 구한다. 
        // 속도(속력) = 거리 / 시간 
        // Time.deltaTime => Time.fixedDeltaTime 으로 계산이 된다 이유는 상위 메소드가 FixedUpdate() 이기 때문에
        characterController.Move(velocity * Time.deltaTime); // World Space 기준으로 현재위치에서 이동하도록 

        if(characterController.isGrounded) 
            currentVelocityY = 0;
        
        
    }

    // 플레이어가 플레이어 카메라 방향으로 회전하는 메소드
    // Y축만 신경을 쓰면 된다.
    public void Rotate()
    {
        //var targetRotation = followCam.transform.rotation.eulerAngles.y;
        // 약식
        var targetRotation = followCam.transform.eulerAngles.y;

        // Damping 처리
        // SmoothDampAngle 에서 + 값과 - 값이 다르지만 위치는 같을 수 있다. 

        // https://www.youtube.com/watch?v=_elaxisPYHU
        // transform.eulerAngles = new Vector3(0,0,0);
        // vs
        // transform.rotation = Quaternion.Euler(0,0,0);

        targetRotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref turnSmoothVelocity, turnSmoothTime);

        // 오일러각을 입력을 하면 자동으로 쿼터니언값으로 변경이 된다.
        // https://m.blog.naver.com/PostView.nhn?blogId=wjsdldks&logNo=220371127320&proxyReferer=https%3A%2F%2Fwww.google.co.kr%2F
        // transform.eulerAngles 를 사용하는 것은 자제할 것으로 나오는데 확인이 필요하구나 
        transform.eulerAngles = Vector3.up * targetRotation; 
        //transform.Rotate(new Vector3(0, targetRotation, 0));

    }

    public void Jump()
    {
        if(!characterController.isGrounded)
            return;

        currentVelocityY = jumpVelocity;


    }

    // 사용자 입력에 따른 Animation Update 메소드
    private void UpdateAnimation(Vector2 moveInput)
    {
        var animationSpeedRate = currentSpeed / speed;

        animator.SetFloat("Vertical Move", moveInput.y * animationSpeedRate, 0.05f, Time.deltaTime);
        animator.SetFloat("Horizontal Move", moveInput.x * animationSpeedRate, 0.05f, Time.deltaTime);
    }
}