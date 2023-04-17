using System.Collections.Generic;
using JS.WorldGeneration;

public class Settlement
{
    public TerrainNode Node { get; private set; }

    public string name { get; private set; }
    public int ID { get; private set; }
    public SettlementType type { get; private set; }
    public HumanoidTribe tribe { get; private set; }
    public int population { get; private set; }
    public bool isSeaFaring { get; private set; }
    public bool isSubterranean { get; private set; }

    public List<TerrainNode> territory;
    public List<TerrainNode> areaOfInfluence;

    private Dictionary<Settlement, int> foreignRelations;

    public Settlement(string name, int ID, TerrainNode node, SettlementType type, HumanoidTribe humanoids, int population)
    {
        this.name = name;
        this.ID = ID;
        Node = node;
        Node.Settlement = this;

        //Set to a seafaring settlement if placed on an island
        isSeaFaring = node.Island != null;
        //Sets the settlement as subterranean if placed in a mountain
        isSubterranean = node.Mountain != null;

        this.type = type;
        tribe = humanoids;
        this.population = population;

        territory = new List<TerrainNode>();
        areaOfInfluence = new List<TerrainNode>();
        foreignRelations = new Dictionary<Settlement, int>();
    }

    public void Relocate(TerrainNode node)
    {
        Node.Settlement = null;
        Node = node;
        Node.Settlement = this;
    }

    public void AdjustSize(SettlementType type, int newPopulation)
    {
        this.type = type;
        population = newPopulation;
    }

    public void AddTerritory(TerrainNode node)
    {
        if (territory.Contains(node)) return;

        territory.Add(node);
        node.Territory = this;
    }

    public void RemoveTerritory(TerrainNode node)
    {
        if (!territory.Contains(node)) return;

        node.Territory = null;
        territory.Remove(node);
    }

    public void AddAreaOfInfluence(TerrainNode node)
    {
        areaOfInfluence.Add(node);
    }

    public void RemoveAreaOfInfluence(TerrainNode node)
    {
        areaOfInfluence.Remove(node);
    }

    public void AddNewRelation(Settlement otherSettlement, int initialDisposition = 0)
    {
        if (!foreignRelations.ContainsKey(otherSettlement))
        {
            foreignRelations.Add(otherSettlement, initialDisposition);
        }
        else
        {
            foreignRelations[otherSettlement] = initialDisposition;
        }
    }

    public void ModifyRelation(Settlement otherSettlement, int dispositionChange)
    {
        if (foreignRelations.ContainsKey(otherSettlement))
        {
            foreignRelations[otherSettlement] += dispositionChange;
        }
        else
        {
            foreignRelations.Add(otherSettlement, dispositionChange);
        }
    }

    public void DeconstructSettlement()
    {
        if (Node != null && Node.Settlement == this) Node.Settlement = null;
        Node = null;

        for (int i = 0; i < territory.Count; i++)
        {
            if (territory[i].Territory == this) territory[i].Territory = null;
        }
        territory.Clear();
        
        areaOfInfluence.Clear();
        foreignRelations.Clear();
    }
}