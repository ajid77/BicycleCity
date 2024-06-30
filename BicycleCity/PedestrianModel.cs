namespace BicycleCity
{
    public class PedestrianModel
    {
        public string ModelName { get; }
        public string ScenarioName { get; }
        public string Scenario { get; internal set; }
        public int Model { get; internal set; }

        public PedestrianModel(string modelName, string scenarioName)
        {
            ModelName = modelName;
            ScenarioName = scenarioName;
        }
    }
}
