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


  Vector3 flockDir, separationDir, steerPos, targetDirection;

  float speed, separationSpeed, sqrVis, sqrSepDist, flockingSqrDist;

  int count;

  Autonomous flockCurr, flockOther, obstCurr;
  Obstacle obstOther;

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

  void Execute(Flock flock)
  {
    flockingSqrDist = 0;
    targetDirection = Vector3.zero;

    flockDir = Vector3.zero;
    separationDir = Vector3.zero;
    steerPos = Vector3.zero;

    speed = 0f;
    separationSpeed = 0f;

    count = 0;

    for (int i = 0; i < flock.numBoids; ++i)
    {
      flockOther = flock.mAutonomous[i];
      
      flockingSqrDist = (
        flockCurr.transform.position - flockOther.transform.position
        ).sqrMagnitude;

      if (flockingSqrDist > sqrVis) continue;

      if (flockCurr == flockOther) continue;

      speed += flockOther.Speed;
      flockDir += flockOther.TargetDirection;
      steerPos += flockOther.transform.position;
      count++;

      if (flockingSqrDist < sqrSepDist){
        targetDirection = (
          flockCurr.transform.position - flockOther.transform.position
          ).normalized;

        separationDir += targetDirection;
        separationSpeed += (
          (flockCurr.transform.position - flockOther.transform.position).magnitude * 
          flock.weightSeparation
        );
      }
    }

    if (count > 0)
    {
      speed = speed / count;
      flockDir = flockDir / count;
      flockDir.Normalize();

      steerPos = steerPos / count;
    }

    flockCurr.TargetDirection =
      flockDir * speed * (flock.useAlignmentRule ? flock.weightAlignment : 0.0f) +
      separationDir * separationSpeed * (flock.useSeparationRule ? flock.weightSeparation : 0.0f) +
      (steerPos - flockCurr.transform.position) * (flock.useCohesionRule ? flock.weightCohesion : 0.0f);
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

          sqrVis = flock.visibility * flock.visibility;
          sqrSepDist = flock.separationDistance * flock.separationDistance;

          for (int i = 0; i < flock.numBoids; ++i){
            flockCurr = flock.mAutonomous[i];
            Execute(flock);
            if(i % BatchSize == 0) yield return null;
          }

          yield return null;
        }
      }
      yield return new WaitForSeconds(TickDuration);
    }
  }

  IEnumerator Coroutine_SeparationWithEnemies()
  {
    Autonomous curr = null;
    Autonomous enemy = null;

    float sqrDist = 0f;
    float sqrSepDist = 0f;

    Vector3 targetDirection = Vector3.zero;

    while (true)
    {
      for(int a = 0; a < nonPredators.Count; ++a){
        curr = nonPredators[a].BoidAutono;
        sqrSepDist = nonPredators[a].BoidFlock.enemySeparationDistance * nonPredators[a].BoidFlock.enemySeparationDistance;

        for(int b = 0; b < predators.Count; ++b){
          enemy = nonPredators[b].BoidAutono;

          sqrDist = (
            enemy.transform.position -
            curr.transform.position).sqrMagnitude;
          
          if (sqrDist < sqrSepDist){
            targetDirection = (
              curr.transform.position - enemy.transform.position
              ).normalized;

            curr.TargetDirection += targetDirection;
            curr.TargetDirection.Normalize();

            curr.TargetSpeed += (
              (enemy.transform.position - curr.transform.position).magnitude * 
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
    float sqrDist = 0f;
    float sqrAvoidRad = 0f;

    Flock flock = null;

    while (true)
    {
      for (int a = 0; a < flocks.Length; ++a)
      {
        flock = flocks[a];
        if (!flock.useAvoidObstaclesRule) continue;

        for (int i = 0; i < flock.numBoids; ++i)
        {
          obstCurr = flocks[a].mAutonomous[i];

          for (int j = 0; j < mObstacles.Length; ++j)
          {
            obstOther = mObstacles[j];
            sqrAvoidRad = obstOther.AvoidanceRadius * obstOther.AvoidanceRadius;
            sqrDist = (obstOther.transform.position - obstCurr.transform.position).sqrMagnitude;

            if (sqrDist > sqrAvoidRad) continue;
            
            Vector3 targetDirection = (
              obstCurr.transform.position -
              obstOther.transform.position).normalized;

            obstCurr.TargetDirection += targetDirection * flock.weightAvoidObstacles;
            obstCurr.TargetDirection.Normalize();
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
      for (int i = 0; i < Obstacles.Length; ++i)
      {
        Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
        float rand = Random.Range(0.0f, 1.0f);
        autono.TargetDirection.Normalize();
        float angle = Mathf.Atan2(autono.TargetDirection.y, autono.TargetDirection.x);

        if (rand > 0.5f)
        {
          angle += Mathf.Deg2Rad * 45.0f;
        }
        else
        {
          angle -= Mathf.Deg2Rad * 45.0f;
        }
        Vector3 dir = Vector3.zero;
        dir.x = Mathf.Cos(angle);
        dir.y = Mathf.Sin(angle);

        autono.TargetDirection += dir * 0.1f;
        autono.TargetDirection.Normalize();
        //Debug.Log(autonomousList[i].TargetDirection);

        float speed = Random.Range(1.0f, autono.MaxSpeed);
        autono.TargetSpeed += speed;
        autono.TargetSpeed /= 2.0f;
      }
      yield return new WaitForSeconds(2.0f);
    }
  }
  IEnumerator Coroutine_Random()
  {
    while (true)
    {
      foreach (Flock flock in flocks)
      {
        if (flock.useRandomRule)
        {
          List<Autonomous> autonomousList = flock.mAutonomous;
          for (int i = 0; i < autonomousList.Count; ++i)
          {
            float rand = Random.Range(0.0f, 1.0f);
            autonomousList[i].TargetDirection.Normalize();
            float angle = Mathf.Atan2(autonomousList[i].TargetDirection.y, autonomousList[i].TargetDirection.x);

            if (rand > 0.5f)
            {
              angle += Mathf.Deg2Rad * 45.0f;
            }
            else
            {
              angle -= Mathf.Deg2Rad * 45.0f;
            }
            Vector3 dir = Vector3.zero;
            dir.x = Mathf.Cos(angle);
            dir.y = Mathf.Sin(angle);

            autonomousList[i].TargetDirection += dir * flock.weightRandom;
            autonomousList[i].TargetDirection.Normalize();
            //Debug.Log(autonomousList[i].TargetDirection);

            float speed = Random.Range(1.0f, autonomousList[i].MaxSpeed);
            autonomousList[i].TargetSpeed += speed * flock.weightSeparation;
            autonomousList[i].TargetSpeed /= 2.0f;
          }
        }
        //yield return null;
      }
      yield return new WaitForSeconds(TickDurationRandom);
    }
  }
  IEnumerator Rule_CrossBorder_Obstacles()
  {
    Autonomous autono = null;

    for(;;){
      for (int i = 0; i < mObstAutono.Length; ++i)
      {
        autono = mObstAutono[i];
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
    Flock flock = null;

    Autonomous curr = null;

    for(;;){
      for (int a = 0; a < flocks.Length; ++a)
      {
        flock = flocks[a];
        for (int i = 0; i < flock.numBoids; ++i)
        {
          curr = flock.mAutonomous[i];
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
