using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
  public float AvoidanceRadiusMultFactor = 1.5f;
  public float SqrAvoidanceRadius
  {
    get
    {
      return (mCollider.radius * 3 * AvoidanceRadiusMultFactor) * (mCollider.radius * 3 * AvoidanceRadiusMultFactor);
    }
  }

  public CircleCollider2D mCollider;
}
