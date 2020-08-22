using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace SoulsHksEditor
{
	[XmlRoot("hkpackfile")]
	public class hkpackfileT
	{
		public class hksectionT
		{
			public class hkobjectT
			{
				public class hkparamT
				{
					[XmlAttribute("name")]
					public string Name;

					[XmlAttribute("numelements")]
					public string NumElements;

					[XmlIgnore]
					public string Content;

					[XmlIgnore]
					public bool IsArray { get { return NumElements != null; } }

					[XmlIgnore]
					public List<string> Elements;

					[XmlElement("hkobject")]
					public List<hkobjectT> hkobject;

					[XmlText]
					public XmlNode[] CDataContent
					{
						get
						{
							XmlDocument dummy = new XmlDocument();

							return new XmlNode[] { dummy.CreateCDataSection(Content) };
						}
						set
						{
							if (value == null)
							{
								Content = null;
								return;
							}

							Content = value.Single().Value;

							if (IsArray)
							{
								Elements = new List<string>(Content.Split(new[] { '\r', '\n' }, options: StringSplitOptions.RemoveEmptyEntries));
							}
						}
					}
				}

				[XmlElement("hkparam")]
				public List<hkparamT> hkparam;

				[XmlAttribute("name")]
				public string Name;
				
				[XmlAttribute("signature")]
				public string Signature;
				
				[XmlAttribute("class")]
				public string Class;

				internal T GetParam<T>(string paramName)
				{
					return (T)Convert.ChangeType(GetParam(paramName).Content, typeof(T));
				}

				internal hkparamT GetParam(string paramName)
				{
					return hkparam.Single(param => param.Name == paramName);
				}

				internal IEnumerable<T> GetParamArray<T>(string paramName)
				{
					var param = GetParam(paramName);

					if (!param.IsArray)
					{
						throw new InvalidOperationException($"{paramName} is not an array");
					}

					return param.Elements.Select(e => (T)Convert.ChangeType(e, typeof(T)));
				}
			}

			[XmlElement("hkobject")]
			public List<hkobjectT> hkobject;
		}

		[XmlElement("hksection")]
		public List<hksectionT> hksection;
	}

	public class HksDataParser
	{
		public class ParsedHksData
		{
			public IEnumerable<StateMachine> cStateMachines;

			public ParsedHksData(IEnumerable<StateMachine> cStateMachines)
			{
				this.cStateMachines = cStateMachines;
			}

			public class StateMachine
			{
				public class State
				{
					public class Transition
					{
						public State targetState;
						private hkpackfileT.hksectionT.hkobjectT Raw;
						public int targetStateId;

						public Transition(hkpackfileT.hksectionT.hkobjectT trans)
						{
							Raw = trans;

							targetStateId = trans.GetParam<int>("toStateId");
						}
					}

					public State(hkpackfileT.hksectionT.hkobjectT obj)
					{
						Raw = obj;

						StateId = obj.GetParam<int>("stateId");


					}

					public int StateId;

					public IEnumerable<Transition> transitions;

					public hkpackfileT.hksectionT.hkobjectT Raw { get; private set; }

					internal void ApplyTransitions(IDictionary<string, hkpackfileT.hksectionT.hkobjectT> dictObjects, IDictionary<hkpackfileT.hksectionT.hkobjectT, IEnumerable<Transition>> cTransitions)
					{
						string transitionsObjName = Raw.GetParam<string>("transitions");
						
						if (transitionsObjName == null || transitionsObjName == "null")
						{
							return;
						}

						var transitionsObj = dictObjects[transitionsObjName];

						transitions = cTransitions[transitionsObj].ToArray();
					}

					internal void ApplyTransitions2(StateMachine stateMachine)
					{
						if (transitions == null)
						{
							return;
						}

						foreach(var transition in transitions)
						{
							transition.targetState = stateMachine.GetStateById(transition.targetStateId);
						}
					}
				}

				private State GetStateById(int targetStateId)
				{
					return states.Single(state => state.StateId == targetStateId);
				}

				public IEnumerable<State> states;

				public IEnumerable<State.Transition> wildcardTransitions;
				private hkpackfileT.hksectionT.hkobjectT Raw;

				public StateMachine(hkpackfileT.hksectionT.hkobjectT stateMachine, IDictionary<string, hkpackfileT.hksectionT.hkobjectT> dictObjects, IDictionary<hkpackfileT.hksectionT.hkobjectT, IEnumerable<State.Transition>> cTransitions)
				{
					this.Raw = stateMachine;

					var stateObjs = stateMachine.GetParamArray<string>("states").Select(stateObjName => dictObjects[stateObjName]);

					states = stateObjs.Select(state => new State(state)).ToArray(); // stateObjs.Select(stateObj => states[stateObj.GetParam<int>("stateId")]);

					string wildcardTransitionsObjName = stateMachine.GetParam<string>("wildcardTransitions");
					if (wildcardTransitionsObjName != null && wildcardTransitionsObjName != "null")
					{
						var wildcardTransitionObject = dictObjects[wildcardTransitionsObjName];

						wildcardTransitions = cTransitions[wildcardTransitionObject].ToArray();

						foreach (var transition in wildcardTransitions)
						{
							transition.targetState = GetStateById(transition.targetStateId);
						}
					}

					foreach (var state in states)
					{
						state.ApplyTransitions(dictObjects, cTransitions);
					}

					foreach (var state in states)
					{
						state.ApplyTransitions2(this);
					}
				}


			}
		}

		public static ParsedHksData Parse(hkpackfileT file)
		{
			var objects = file.hksection.Single().hkobject;

			IDictionary<string, hkpackfileT.hksectionT.hkobjectT> objectsMap = objects.ToDictionary(obj=>obj.Name);

			var stateMachines = objects.Where(obj => obj.Class == "hkbStateMachine");

			var states = objects.Where(obj => obj.Class == "hkbStateMachineStateInfo");
		
			var transitions = objects.Where(obj => obj.Class == "hkbStateMachineTransitionInfoArray");

			var cStates = states.Select(state => new ParsedHksData.StateMachine.State(state)).ToArray();

			//.Select(obj => obj.GetParam<int>("toStateId")).Select(targetStateId => dictStates[targetStateId]))


			var cTransitions = transitions.Select(trans => (trans, trans.GetParam("transitions").hkobject)).Select(t => (t.trans, t.hkobject.Select(trans => new ParsedHksData.StateMachine.State.Transition(trans)))).ToDictionary(t => t.trans, t => t.Item2);

			var cStateMachines = stateMachines.Select(stateMachine => new ParsedHksData.StateMachine(stateMachine, objectsMap, cTransitions));

			return new ParsedHksData(cStateMachines);
		}
	}
}
