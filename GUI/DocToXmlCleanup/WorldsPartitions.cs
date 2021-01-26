using System;
using System.Collections.Generic;
using System.Text;

namespace DocToXMLCleanup
{
        /// <summary>
    /// Holds information on multiple worlds (names, descriptions) and their children partitions (names, descriptions),
    /// e.g., gender world might have male and female as children partitions, or 
    /// "native language" world might have English, Spanish, German, etc. as children partitions.
    /// Also holds information on "open ended collection scope worlds" whose values may have a large range,
    /// e.g., favorite food, and which apply to the collection level scope.
    /// </summary>
    public class WorldsPartitions
    {
        public List<World> MemberWorlds;
        public List<CollectionScopeWorld> OpenEndedCollectionScopeWorlds;
    }

    /// <summary>
    /// Holds information on single world: name and description, and children partitions (names and descriptions).
    /// </summary>
    public class World
    {
        public string Name;
        public string Desc;
        public List<Partition> ChildPartitions;
    }

    /// <summary>
    /// Holds information on single partition: name, description.
    /// </summary>
    public class Partition
    {
        public string Name;
        public string Desc;
    }

    /// <summary>
    /// Holds information on single collection-scope world: name and description and NO partitions (assuming broad range of possible values).
    /// </summary>
    public class CollectionScopeWorld
    {
        public string Name;
        public string Desc;
    }

}
