using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Autonomous : MonoBehaviour
{
  public float MaxSpeed = 10.0f;

  public float Speed = 0.0f;

  public float TargetSpeed = 0.0f;
  
  public float RotationSpeed = 0.0f;

  public Vector2 accel = new Vector2(0.0f, 0.0f);

  public Vector3 TargetDirection = Vector3.zero;

  public SpriteRenderer spriteRenderer;

  // Start is called before the first frame update
  void Start()
  {
    Vector2 dir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * 30f), Mathf.Sin(Mathf.Deg2Rad * 30f));
    dir.Normalize();
    TargetDirection = dir;
  }

  // Update is called once per frame
  public void Update()
  {
    Vector3 targetDirection = TargetDirection;
    targetDirection.Normalize();

    Quaternion targetRotation = Quaternion.LookRotation(
      forward: Vector3.forward,
      upwards: Quaternion.Euler(0, 0, 90) * targetDirection.normalized
      );

    transform.rotation = Quaternion.RotateTowards(
      transform.rotation, 
      targetRotation, 
      RotationSpeed * Time.deltaTime);

    Speed += ((TargetSpeed - Speed)/10.0f) * Time.deltaTime;

    if (Speed > MaxSpeed) Speed = MaxSpeed;

    transform.Translate(Vector3.right * Speed * Time.deltaTime, Space.Self);
  }
}
