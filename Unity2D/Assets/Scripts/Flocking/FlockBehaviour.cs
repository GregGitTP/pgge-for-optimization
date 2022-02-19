using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FlockBehaviour : MonoBehaviour
{
  struct Boid{
    public Flock BoidFlock;
    public Autonomous BoidAutono;
  }

  [SerializeField]
  GameObject[] Obstacles;

  Obstacle[] mObstacles;
  Autonomous[] mObstAutono;

  [SerializeField]
  BoxCollider2D Bounds;

  public float TickDuration = 1.0f;
  public float TickDurationSeparationEnemy = 0.1f;
  public float TickDurationRandom = 1.0f;

  public int BoidIncr = 100;
  public bool useFlocking = false;
  public int BatchSize = 100;

  public Flock[] flocks;

  List<Boid> predators;
  List<Boid> nonPredators;

  void Awake(){
    mObstacles = new Obstacle[Obstacles.Length];
    mObstAutono = new Autonomous[Obstacles.Length];

    predators = new List<Boid>();
    nonPredators = new List<Boid>();
  }

  void Start()
  {
    // Randomize obstacles placement.
    for(int i = 0; i < Obstacles.Length; ++i)
    {
      float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
      float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
      Obstacles[i].transform.position = new Vector3(x, y, 0.0f);
      Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
      Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
      autono.MaxSpeed = 1.0f;
      obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
      mObstacles[i] = obs;
      mObstAutono[i] = autono;
    }

    foreach (Flock flock in flocks)
    {
      CreateFlock(flock);
    }

    StartCoroutine(Coroutine_Flocking());

    StartCoroutine(Coroutine_Random());
    StartCoroutine(Coroutine_AvoidObstacles());
    StartCoroutine(Coroutine_SeparationWithEnemies());
    StartCoroutine(Coroutine_Random_Motion_Obstacles());

    StartCoroutine(Rule_CrossBorder());
    StartCoroutine(Rule_CrossBorder_Obstacles());
  }

  void CreateFlock(Flock flock)
  {
    for(int i = 0; i < flock.numBoids; ++i)
    {
      float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
      float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

      AddBoid(x, y, flock);
    }
  }

  void Update()
  {
    HandleInputs();
  }

  void HandleInputs()
  {
    if (EventSystem.current.IsPointerOverGameObject() ||
       enabled == false)
    {
      return;
    }

    if (Input.GetKeyDown(KeyCode.Space))
    {
      AddBoids(BoidIncr);
    }
  }

  void AddBoids(int count)
  {
    for(int i = 0; i < count; ++i)
    {
      float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
      float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

      AddBoid(x, y, flocks[0]);
    }
    flocks[0].numBoids += count;
  }

  void AddBoid(float x, float y, Flock flock)
  {
    GameObject obj = Instantiate(flock.PrefabBoid);
    obj.name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
    obj.transform.position = new Vector3(x, y, 0.0f);
    Autonomous boid = obj.GetComponent<Autonomous>();
    flock.mAutonomous.Add(boid);
    boid.MaxSpeed = flock.maxSpeed;
    boid.RotationSpeed = flock.maxRotationSpeed;

    if(flock.isPredator){
      predators.Add(new Boid{BoidFlock = flock, BoidAutono = boid});
    }
    else{
      nonPredators.Add(new Boid{BoidFlock = flock, BoidAutono = boid});
    }
  }

  void Execute(Flock flock, int i)
  {
    Autonomous curr = flock.mAutonomous[i];

    Vector3 flockDir = Vector3.zero;
    Vector3 separationDir = Vector3.zero;
    Vector3 steerPos = Vector3.zero;

    Vector3 alignment = Vector3.zero;
    Vector3 seperation = Vector3.zero;
    Vector3 cohesion = Vector3.zero;

    float speed = 0f;
    float separationSpeed = 0f;

    float sqrVis = flock.visibility * flock.visibility;
    float sqrSepDist = flock.separationDistance * flock.separationDistance;

    int count = 0;

    for (int j = 0; j < flock.numBoids; ++j)
    {
      Autonomous other = flock.mAutonomous[j];
      
      float sqrDist = (
        curr.transform.position - other.transform.position
        ).sqrMagnitude;

      if (sqrDist >= sqrVis) continue;

      if (curr == other) continue;

      speed += other.Speed;
      flockDir += other.TargetDirection;
      steerPos += other.transform.position;
      count++;

      if (sqrDist < sqrSepDist){
        Vector3 targetDirection = (
          curr.transform.position - other.transform.position
          );
        targetDirection.Normalize();

        separationDir += targetDirection;
        separationSpeed += (
          Mathf.Sqrt(sqrDist) * 
          flock.weightSeparation
        );
      }
    }

    if (count > 0)
    {
      speed /= count;
      flockDir /= count;
      flockDir.Normalize();

      steerPos /= count;
    }

    if(flock.useAlignmentRule) 
      alignment = flockDir * speed * flock.weightAlignment;

    if(flock.useSeparationRule) 
      seperation = separationDir * separationSpeed * flock.weightSeparation;

    if(flock.useCohesionRule)
      cohesion = (steerPos - curr.transform.position) * flock.weightCohesion;

    curr.TargetDirection = alignment + seperation + cohesion;
  }


  IEnumerator Coroutine_Flocking()
  {
    while (true)
    {
      if (useFlocking)
      {
        for (int a = 0; a < flocks.Length; ++a)
        {
          Flock flock = flocks[a];

          for (int i = 0; i < flock.numBoids; ++i){
            Execute(flock, i);
            if(i % BatchSize == 0) yield return null;
          }
        }
      }
      yield return new WaitForSeconds(TickDuration);
    }
  }

  IEnumerator Coroutine_SeparationWithEnemies()
  {
    while (true)
    {
      for(int a = 0; a < nonPredators.Count; ++a){
        Autonomous curr = nonPredators[a].BoidAutono;
        float sqrSepDist = nonPredators[a].BoidFlock.enemySeparationDistance * nonPredators[a].BoidFlock.enemySeparationDistance;

        for(int b = 0; b < predators.Count; ++b){
          Autonomous enemy = nonPredators[b].BoidAutono;

          float sqrDist = (
            enemy.transform.position -
            curr.transform.position).sqrMagnitude;
          
          if (sqrDist < sqrSepDist){
            Vector3 targetDirection = (
              curr.transform.position - enemy.transform.position
              );

            curr.TargetDirection += targetDirection;
            curr.TargetDirection.Normalize();

            curr.TargetSpeed += (
              Mathf.Sqrt(sqrDist) * 
              nonPredators[a].BoidFlock.weightFleeOnSightEnemy
            );
            curr.TargetSpeed /= 2.0f;
          }
        }
      }
      yield return new WaitForSeconds(.2f);
    }
  }

  IEnumerator Coroutine_AvoidObstacles()
  {
    while (true)
    {
      for (int a = 0; a < flocks.Length; ++a)
      {
        Flock flock = flocks[a];
        if (!flock.useAvoidObstaclesRule) continue;

        for (int i = 0; i < flock.numBoids; ++i)
        {
          Autonomous curr = flock.mAutonomous[i];

          for (int j = 0; j < mObstacles.Length; ++j)
          {
            Obstacle other = mObstacles[j];
            float sqrAvoidRad = other.SqrAvoidanceRadius;
            float sqrDist = (other.transform.position - curr.transform.position).sqrMagnitude;

            if (sqrDist >= sqrAvoidRad) continue;
            
            Vector3 targetDirection = (
              curr.transform.position -
              other.transform.position);

            curr.TargetDirection += targetDirection * flock.weightAvoidObstacles;
            curr.TargetDirection.Normalize();
          }
        }
      }
      yield return new WaitForSeconds(.2f);
    }
  }
  IEnumerator Coroutine_Random_Motion_Obstacles()
  {
    while (true)
    {
      for (int i = 0; i < mObstAutono.Length; ++i)
      {
        Autonomous autono = mObstAutono[i];
        float rand = Random.Range(0.0f, 1.0f);
        float angle = Mathf.Atan2(autono.TargetDirection.y, autono.TargetDirection.x);

        if (rand > 0.5f)
        {
          angle += Mathf.Deg2Rad * 45.0f;
        }
        else
        {
          angle -= Mathf.Deg2Rad * 45.0f;
        }

        Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

        autono.TargetDirection += dir * 0.1f;
        autono.TargetDirection.Normalize();

        float speed = Random.Range(1.0f, autono.MaxSpeed);
        autono.TargetSpeed += speed;
        autono.TargetSpeed /= 2.0f;
      }
      yield return new WaitForSeconds(2f);
    }
  }
  IEnumerator Coroutine_Random()
  {
    while (true)
    {
      for (int a = 0; a < flocks.Length; ++a)
      {
        Flock flock = flocks[a];
        if (flock.useRandomRule)
        {
          for (int i = 0; i < flock.numBoids; ++i)
          {
            Autonomous curr = flock.mAutonomous[i];
            float rand = Random.Range(0.0f, 1.0f);
            float angle = Mathf.Atan2(curr.TargetDirection.y, curr.TargetDirection.x);

            if (rand > 0.5f)
            {
              angle += Mathf.Deg2Rad * 45.0f;
            }
            else
            {
              angle -= Mathf.Deg2Rad * 45.0f;
            }
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));

            curr.TargetDirection += dir * flock.weightRandom;
            curr.TargetDirection.Normalize();

            float speed = Random.Range(1.0f, curr.MaxSpeed);
            curr.TargetSpeed += speed * flock.weightSeparation;
            curr.TargetSpeed /= 2.0f;
          }
        }
      }
      yield return new WaitForSeconds(TickDurationRandom);
    }
  }
  IEnumerator Rule_CrossBorder_Obstacles()
  {
    for(;;){
      for (int i = 0; i < mObstAutono.Length; ++i)
      {
        Autonomous autono = mObstAutono[i];
        Vector3 pos = autono.transform.position;

        if (pos.x > Bounds.bounds.max.x)
        {
          pos.x = Bounds.bounds.min.x;
        }
        else if (pos.x < Bounds.bounds.min.x)
        {
          pos.x = Bounds.bounds.max.x;
        }
        if (pos.y > Bounds.bounds.max.y)
        {
          pos.y = Bounds.bounds.min.y;
        }
        else if (pos.y < Bounds.bounds.min.y)
        {
          pos.y = Bounds.bounds.max.y;
        }
        autono.transform.position = pos;
      }
      yield return new WaitForSeconds(1f);
    }
  }

  IEnumerator Rule_CrossBorder()
  {
    for(;;){
      for (int a = 0; a < flocks.Length; ++a)
      {
        Flock flock = flocks[a];
        for (int i = 0; i < flock.numBoids; ++i)
        {
          Autonomous curr = flock.mAutonomous[i];
          Vector3 pos = curr.transform.position;

          if(flock.bounceWall){
            if (pos.x + 5.0f > Bounds.bounds.max.x)
            {
              curr.TargetDirection.x = -1.0f;
            }
            if (pos.x - 5.0f < Bounds.bounds.min.x)
            {
              curr.TargetDirection.x = 1.0f;
            }
            if (pos.y + 5.0f > Bounds.bounds.max.y)
            {
              curr.TargetDirection.y = -1.0f;
            }
            else if (pos.y - 5.0f < Bounds.bounds.min.y)
            {
              curr.TargetDirection.y = 1.0f;
            }

            curr.TargetDirection.Normalize();
          }
          else{
            if (pos.x > Bounds.bounds.max.x)
            {
              pos.x = Bounds.bounds.min.x;
            }
            if (pos.x < Bounds.bounds.min.x)
            {
              pos.x = Bounds.bounds.max.x;
            }
            if (pos.y > Bounds.bounds.max.y)
            {
              pos.y = Bounds.bounds.min.y;
            }
            else if (pos.y < Bounds.bounds.min.y)
            {
              pos.y = Bounds.bounds.max.y;
            }

            curr.transform.position = pos;
          }
        }
      }
      yield return new WaitForSeconds(.2f);
    }
  }
}
