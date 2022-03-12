using System.Collections.Generic;

namespace versoft.module_manager
{
    public class Dependencies
    {
        public Dictionary<string, string> dependencies = new Dictionary<string, string>();
        public Dependencies()
        {
            dependencies = new Dictionary<string, string>();
        }

        public Dependencies(Dependencies baseDependencies) : base()
        {
            if (baseDependencies == null || baseDependencies.dependencies == null) { return; }

            foreach(var pair in baseDependencies.dependencies)
            {
                dependencies.Add(pair.Key, pair.Value);
            }
        }
    }
}