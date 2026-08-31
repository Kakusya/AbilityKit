namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Typed arguments for the template DSL emit action.
    /// </summary>
    public readonly struct EmitArgs
    {
        public readonly int EmitterId;

        public EmitArgs(int emitterId)
        {
            EmitterId = emitterId;
        }
    }
}
