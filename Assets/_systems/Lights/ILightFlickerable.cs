public interface ILightFlickerable
{
	FlickerableLightType GetLightType();
	void TurnOn();
	void TurnOff();
	void FlickerOn(float duration);
	void FlickerOff(float duration);
	void StartContinuousFlicker();
	void StopContinuousFlicker();
}