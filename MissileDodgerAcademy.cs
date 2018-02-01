public class MissileDodgerAcademy : Academy {
  public float GoalTime;
  public float MissileAmount;
  public float MissileFuelAmount;
  public float MissileSpeed;

  public override void InitializeAcademy() {
    Monitor.verticalOffset = 1f;
  }

  public override void AcademyReset() {
    GoalTime = (float)resetParameters["GoalTime"];
    MissileAmount = (float)resetParameters["MissileAmount"];
    MissileFuelAmount = (float)resetParameters["MissileFuelAmount"];
    MissileSpeed = (float)resetParameters["MissileSpeed"];
  }

  public override void AcademyStep() {
  }
}