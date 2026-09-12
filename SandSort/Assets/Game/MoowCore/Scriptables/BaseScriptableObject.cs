using UnityEngine;

namespace Moow {
	abstract public class BaseScriptableObject : ScriptableObject {

		virtual public int getSaveDataOrderId => -1;

		virtual public void load() {
			// Override at the subclass.
		}

		virtual public void save() {
			// Override at the subclass.
		}

		virtual public void reset() {
			// Override at the subclass.
		}
		// Indexer Syntax
		public object this[string fieldName] {
			get { return GetType().GetField(fieldName).GetValue(this); }
			set { GetType().GetField(fieldName).SetValue(this, value); }
		}

		virtual public void updateWithRemoteConfig() {
			// Override at the subcless
		}
	}
}
