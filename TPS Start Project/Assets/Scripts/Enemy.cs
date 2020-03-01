using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Enemy : LivingEntity
{
    private enum State
    {
        Patrol,
        Tracking,
        AttackBegin,
        Attacking
    }
    
    private State state;
    
    private NavMeshAgent agent;
    private Animator animator;

    public Transform attackRoot; // 공격을 위한 Pivot 포인트
    public Transform eyeTransform; // 공격대상에 대한 감지를 위한 시야 기준점 (눈의 위치)
    
    private AudioSource audioPlayer;
    public AudioClip hitClip;
    public AudioClip deathClip;
    
    private Renderer skinRenderer;

    public float runSpeed = 10f;
    [Range(0.01f, 2f)] public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;
    
    public float damage = 30f;
    public float attackRadius = 2f;
    private float attackDistance;
    
    public float fieldOfView = 50f;     // 시야각 
    public float viewDistance = 10f;     // 볼 수 있는 거리   
    public float patrolSpeed = 3f;  // 정찰 속도
    
    //[HideInInspector] 
    public LivingEntity targetEntity;     // 자신이 추적할 대상,
    public LayerMask whatIsTarget;


    private RaycastHit[] hits = new RaycastHit[10];
    private List<LivingEntity> lastAttackedTargets = new List<LivingEntity>();      // 공격을 할 때 초기화되는 리스트, 공격 직전까지 담아 놓은 리스트
    
    private bool hasTarget => targetEntity != null && !targetEntity.dead;       // 추적할 상대방이 존재하는지

    

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        // 시야와 공격의 범위 지정
        if(attackRoot != null) {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(attackRoot.position, attackRadius);
        }

        if(eyeTransform != null) {
            var leftEyeRotation = Quaternion.AngleAxis(-fieldOfView * 0.5f, Vector3.up);
            var leftRayDirection = leftEyeRotation * transform.forward;
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawSolidArc(eyeTransform.position, Vector3.up, leftRayDirection, fieldOfView, viewDistance);

        }
    }
    
#endif
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioPlayer = GetComponent<AudioSource>();
        skinRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        var attackPivot = attackRoot.position;
        attackPivot.y = transform.position.y;
        attackDistance = Vector3.Distance(transform.position, attackPivot) + attackRadius;

        attackDistance += agent.radius;
        agent.stoppingDistance = attackDistance;
        agent.speed = patrolSpeed;

    }

    // 생성 제너레이터를 통해 실행되는 메소드
    public void Setup(float health, float damage, float runSpeed, float patrolSpeed, Color skinColor)
    {
        this.startingHealth = health;
        this.health = health;

        this.damage = damage;
        this.runSpeed = runSpeed;
        this.patrolSpeed = patrolSpeed;

        skinRenderer.material.color = skinColor;
        agent.speed = patrolSpeed;
    }

    private void Start()
    {
        StartCoroutine(UpdatePath());
    }

    private void Update()
    {
        if(dead) {
            return;
        }

        if(state == State.Tracking){
            if(Vector3.Distance(targetEntity.transform.position, transform.position) <= attackDistance) {
                BeginAttack();
            }
        }

        //desiredVelocity 는 agent가 설정에 따라 반환되는 값으로 
        //장애물이나 상황에 따라 설정 값이 아닌 다른 값을 받을 수 있다. NavMeshAgent에서 반환되는 값
        animator.SetFloat("Speed", agent.desiredVelocity.magnitude);
    }

    private void FixedUpdate()
    {
        if (dead) 
            return;

        //AttackBegin, Attacking 상태변경 텀을 주어서 처리를 한다.
        //값의 통제는 Animation 에서 한다.
        if(state == State.AttackBegin || state == State.Attacking) {
            var lookRotation = Quaternion.LookRotation(targetEntity.transform.position - transform.position, Vector3.up);
            var targetAngleY = lookRotation.eulerAngles.y;

            // 감지가 되었다면 대상을 바라보도록 회전을 하는데 Damping 를 부여한다.
            targetAngleY = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngleY, ref turnSmoothVelocity, turnSmoothTime);
            transform.eulerAngles = Vector3.up * targetAngleY;
        }

        // *******************************************************************
        // 상대방을 감지하고 공격하는 내용이 담김
        // Physics.CastSphere 
        // Cast 계정의 함수는 이전위치에서 새위치로 이동할때 프레임으로 체크하는 것이 아닌
        // 연속성으로 감지한다. 
        // 가장 어려운 부분 2-18 7분 부분 다시 볼껏
        // SphereCast 은 포함되는 한개
        // SphereCastAll 은 포함되는 여러개
        // SphereCastNonAlloc 그 안에 겹치지 않게(?) => Sweeping Test
        // 움직이면서 상대를 감지하기 위해서 
        // *******************************************************************
        if(state == State.Attacking) {
            var direction = transform.forward;
            var deltaDistance = agent.velocity.magnitude * Time.deltaTime;
            
            var size = Physics.SphereCastNonAlloc(attackRoot.position, attackRadius, direction, hits, deltaDistance, whatIsTarget);

            // size 이외의 나머지 배열의 값은 직전 프레임의 값으로 이러져 있다. 
            for(var i = 0; i < size; i++) {
                var attackTargetEntity = hits[i].collider.GetComponent<LivingEntity>();
                if(attackTargetEntity != null && !lastAttackedTargets.Contains(attackTargetEntity)) {
                    var message = new DamageMessage();
                    message.amount = damage;
                    message.damager = gameObject;
                    // 처음 시작하는 값은 distancd 값이 0이 나오기 때문에 처리하는 로직
                    if(hits[i].distance <= 0f) {
                        message.hitPoint = attackRoot.position;
                    } else {
                        message.hitPoint = hits[i].point;
                    }
                    
                    message.hitNormal = hits[i].normal;
                    attackTargetEntity.ApplyDamage(message);
                    lastAttackedTargets.Add(attackTargetEntity);
                    break;
                }
            }
        }
        
    }

    private IEnumerator UpdatePath()
    {
        while (!dead)
        {
            if (hasTarget)
            {
                if(state == State.Patrol) {
                    state = State.Tracking;
                    agent.speed = runSpeed;
                }

                // agent 목표 상대지로 정하고 이동한다. 
                agent.SetDestination(targetEntity.transform.position);
            }
            else
            {
                // targetEntity 가 사망할 경우 다른 대상을 넣을 수 있도록 값을 초기화한다.
                if (targetEntity != null) 
                    targetEntity = null;

                if(state != State.Patrol) {
                    state = State.Patrol;
                    agent.speed = patrolSpeed;
                }

                if(agent.remainingDistance <= 1f) {
                    var patrolTargetPosition = Utility.GetRandomPointOnNavMesh(transform.position, 20f, NavMesh.AllAreas);
                    agent.SetDestination(patrolTargetPosition);    
                }

                // 반지름의 구안에 포함되는 whatIsTarget를 체크하고 반환된 값에서
                // 그 개체가 시야각에 포한되는 살아있는 걔체에 대해서
                var colliders = Physics.OverlapSphere(eyeTransform.position, viewDistance, whatIsTarget);
                foreach(var collider in colliders) {
                    if(!IsTargetOnSight(collider.transform)) {
                        continue;
                        //break; //==> 다른 부분
                    }

                    var livingEntity = collider.GetComponent<LivingEntity>();
                    if(livingEntity != null && !livingEntity.dead) {
                        targetEntity = livingEntity;
                        break;
                    }

                }
            }
             
            yield return new WaitForSeconds(0.05f);
        }
    }
    
    public override bool ApplyDamage(DamageMessage damageMessage)
    {
        if (!base.ApplyDamage(damageMessage)) 
            return false;
        
        if(targetEntity == null) {
            targetEntity = damageMessage.damager.GetComponent<LivingEntity>();
        }

        EffectManager.Instance.PlayHitEffect(damageMessage.hitPoint, damageMessage.hitNormal, transform, EffectManager.EffectType.Flesh);
        audioPlayer.PlayOneShot(hitClip);
        return true;
    }

    // 팔을 휘두르는거와 같이 공격이 총쏘는 것 처럼 한번만 처리되는 것이 아니라
    // 시간 동안 공격이 지속이 된다.
    // 처음부터 공격을 가는 가하는 것이 아니기 때문에 EnableAttack을 만듬
    public void BeginAttack()
    {
        state = State.AttackBegin;

        // ai를 멈춤
        agent.isStopped = true;
        animator.SetTrigger("Attack");
    }
    
    // Zombie Animation의 Bite Status에 이벤트가 있음
    // 쓰기 가능한 Animation Clip만 지정할 수 있다.
    // 즉, fbx 파일 내에 있는 Animation은 지정할 수 없다. 
    public void EnableAttack()
    {
        state = State.Attacking;
        
        lastAttackedTargets.Clear();
    }

    public void DisableAttack()
    {
        if(hasTarget) {
            state = State.Tracking;
        } else {
            state = State.Patrol;
        }
        
        // ai 작동
        agent.isStopped = false;
    }

    // 시야각 안에 개체가 있는지 없는지
    private bool IsTargetOnSight(Transform target)
    {
        RaycastHit hit;
        // 에너미가 바라보고 있는 방향은 상대방 위치에서 현재 위치를 뺀 경우
        var direction = target.position - eyeTransform.position;
        //* 앞쪽방향에서의 y 값
        // y값을 똑같이 수평으로 맞춘다.
        direction.y = eyeTransform.forward.y;

        // 시야각 바깥에 존재하는 경우
        if(Vector3.Angle(direction, eyeTransform.forward) > fieldOfView * 0.5f) {
            return false;
        }

        //direction = target.position - eyeTransform.position; // y 값을 사용함

        // 시야각에 안에는 들어와 있지만 장애물이 아닌 원하는 대상인지 체크
        if(Physics.Raycast(eyeTransform.position, direction, out hit, viewDistance, whatIsTarget)) {
            // 대상과 일치하는지
            if(hit.transform == target) {
                return true;
            }
        }

        return false;
    }
    
    public override void Die()
    {
        base.Die();
        GetComponent<Collider>().enabled = false;
        agent.enabled = false;
        // isStopped를 사용해도 되지만 다른 agent가 길을 피하면서 다니게 된다.
        // 시체가 많이 쌓이게 되면 많이 돌아서 길을 이동할 수 있다.
        //agent.isStopped = true;

        //https://m.blog.naver.com/PostView.nhn?blogId=winterwolfs&logNo=220275247360&proxyReferer=https%3A%2F%2Fwww.google.co.kr%2F
        // 애니메이션 자체로 움직임 (자동)
        animator.applyRootMotion = true;
        animator.SetTrigger("Die");

        if (deathClip != null)
            audioPlayer.PlayOneShot(deathClip);
    }
}