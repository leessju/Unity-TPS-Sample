using System.Resources;
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
    
    [HideInInspector] public LivingEntity targetEntity;     // 자신이 추적할 대상,
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

    }

    private void FixedUpdate()
    {
        if (dead) return;
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

                if(agent.remainingDistance < 1f) {
                    var patrolTargetPosition = Utility.GetRandomPointOnNavMesh(transform.position, 20f, NavMesh.AllAreas);
                    agent.SetDestination(patrolTargetPosition);    
                }

                // 반지름의 구안에 포함되는 whatIsTarget를 체크하고 반환된 값에서
                // 그 개체가 시야각에 포한되는 살아있는 걔체에 대해서
                var colliders = Physics.OverlapSphere(eyeTransform.position, viewDistance, whatIsTarget);
                foreach(var collider in colliders) {
                    if(!IsTargetOnSight(collider.transform)) {
                        continue;
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
        if (!base.ApplyDamage(damageMessage)) return false;
        
        return true;
    }

    public void BeginAttack()
    {
        state = State.AttackBegin;

        agent.isStopped = true;
        animator.SetTrigger("Attack");
    }

    public void EnableAttack()
    {
        state = State.Attacking;
        
        lastAttackedTargets.Clear();
    }

    public void DisableAttack()
    {
        state = State.Tracking;
        
        agent.isStopped = false;
    }

    // 시야각 안에 개체가 있는지 없는지
    private bool IsTargetOnSight(Transform target)
    {
        RaycastHit hit;
        var direction = target.position - eyeTransform.position;
        //* 앞쪽방향에서의 y 값
        direction.y = eyeTransform.forward.y;


        

        return false;
    }
    
    public override void Die()
    {

    }
}