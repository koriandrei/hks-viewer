using DynamicData;
using NodeNetwork.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace SoulsHksEditor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{

			InitializeComponent();

			var myBinding = new Binding("Index")
			{ Source = this, Path = new PropertyPath("Index") };

			label.SetBinding(Label.ContentProperty, myBinding);

			var deserialized = (hkpackfileT)new XmlSerializer(typeof(hkpackfileT)).Deserialize(new FileStream(@"D:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\chr\c0000-behbnd-dcx\Action\c0000\Export\Behaviors\c0000.xml", FileMode.Open));

			parsed = HksDataParser.Parse(deserialized);
			Console.WriteLine(parsed.cStateMachines.Count());
			//Create a new viewmodel for the NetworkView
			network = new NetworkViewModel();
			UpdateNetwork();

			////Create the node for the first node, set its name and add it to the network.
			//var node1 = new NodeViewModel();
			//node1.Name = "Node 1";
			//network.Nodes.Add(node1);

			////Create the viewmodel for the input on the first node, set its name and add it to the node.
			//var node1Input = new NodeInputViewModel();
			//node1Input.Name = "Node 1 input";
			//node1.Inputs.Add(node1Input);

			////Create the second node viewmodel, set its name, add it to the network and add an output in a similar fashion.
			//var node2 = new NodeViewModel();
			//node2.Name = "Node 2";
			//network.Nodes.Add(node2);

			//var node2Output = new NodeOutputViewModel();
			//node2Output.Name = "Node 2 output";
			//node2.Outputs.Add(node2Output);

			//Assign the viewmodel to the view.
			HksView.ViewModel = network;
		}

		private void UpdateNetwork()
		{
			network.Nodes.Clear();
			network.Connections.Clear();

			var stateMachineToDraw = parsed.cStateMachines.ElementAt(Index);

			var states1 = stateMachineToDraw.states.ToDictionary(state => state, state => new NodeViewModel() { Name = state.Raw.GetParam<string>("name") });

			foreach (var state in states1)
			{
				network.Nodes.Add(state.Value);
			}

			var anyStateNode = new NodeViewModel() { Name = "Any State" };

			network.Nodes.Add(anyStateNode);

			if (stateMachineToDraw.wildcardTransitions != null)
			{
				foreach (var trans in stateMachineToDraw.wildcardTransitions)
				{
					var targetStateNode = states1[trans.targetState];

					var output = new NodeOutputViewModel();

					var input = new NodeInputViewModel();

					anyStateNode.Outputs.Add(output);

					targetStateNode.Inputs.Add(input);

					var connection = new ConnectionViewModel(network, input, output);

					network.Connections.Add(connection);
				}
			}

			foreach (var state in states1)
			{
				var originNode = state.Value;

				if (state.Key.transitions != null)
				{
					foreach (var trans in state.Key.transitions)
					{
						var targetStateNode = states1[trans.targetState];

						var output = new NodeOutputViewModel();

						var input = new NodeInputViewModel();

						originNode.Outputs.Add(output);

						targetStateNode.Inputs.Add(input);

						var connection = new ConnectionViewModel(network, input, output);

						network.Connections.Add(connection);
					}
				}
			}
		}

		public int Index { get; set; } = 0;
		private HksDataParser.ParsedHksData parsed;
		private NetworkViewModel network;

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Index--;

			UpdateNetwork();
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			Index++;

			UpdateNetwork();
		}
	}
}
