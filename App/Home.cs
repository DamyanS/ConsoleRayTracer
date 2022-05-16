using RayTracer;
using System.Reflection;

namespace App;

public partial class Home : Form
{
    private const string LOCATION = "worlds.json";

    private readonly List<World> worlds;
    private int _width = 95;
    private int _height = 70;

    public Home()
    {
        JsonConvert.DefaultSettings = () => new()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            TypeNameHandling = TypeNameHandling.Auto,
            ContractResolver = new ConstructorResolver(),
        };

        if (!File.Exists(LOCATION))
        {
            File.WriteAllText(LOCATION, JsonConvert.SerializeObject(new List<World>() { { World.Default } }));
        }

        worlds = JsonConvert.DeserializeObject<List<World>>(File.ReadAllText(LOCATION))!;
        InitializeComponent();
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        new Thread(() => worlds[0].Start(new(Terminal.Windows, Renderer.RayTracer, _width, _height))).Start();
    }

    private void textBoxWidth_TextChanged(object sender, EventArgs e)
    {
        _width = _width switch
        {
            _ when int.TryParse(textBoxWidth.Text, out _width) => _width,
            var prev => prev,
        };
    }

    private void textBoxHeight_TextChanged(object sender, EventArgs e)
    {
        _height = _height switch
        {
            _ when int.TryParse(textBoxHeight.Text, out _height) => _height,
            var prev => prev,
        };
    }

    private void Home_Load(object sender, EventArgs e)
    {
        textBoxWidth.Text = _width.ToString();
        textBoxHeight.Text = _height.ToString();
    }
}

class ConstructorResolver : DefaultContractResolver
{
    private static readonly IEnumerable<Type> _systemTypes = typeof(Assembly).Assembly.GetExportedTypes();

    protected override JsonObjectContract CreateObjectContract(Type objectType)
    {
        var ctor = objectType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .MaxBy(ctor => ctor.GetParameters().Length);

        var contract = base.CreateObjectContract(objectType);
        if (!_systemTypes.Contains(objectType) && ctor is not null)
        {
            contract.OverrideCreator = o => ctor.Invoke(o);
            foreach (var param in CreateConstructorParameters(ctor, contract.Properties))
            {
                if (!contract.CreatorParameters.Any(p => p.PropertyName == param.PropertyName))
                {
                    contract.CreatorParameters.Add(param);
                }
            }
        }
        return contract;
    }
}