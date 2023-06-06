using JS.CharacterSystem;

namespace JS.ECS
{
    public class WorldLocomotion : ComponentBase
    {
        public int MoveSpeed
        {
            get
            {
                if (entity.TryGetStat("MoveSpeed", out StatBase stat))
                {
                    return stat.Value;
                }
                return 1;
            }
        }

        public override void OnEvent(Event newEvent)
        {
            //
        }
    }
}

