using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Fragilem17.MirrorsAndPortals
{
	public class MirrorRendererCullingVolume : MonoBehaviour
	{
		[TextArea]
		public string instructions = "Make sure to have a rigidbody and collider on the mainCamera! For this to work";

		[Space(10)]
		public bool disableAllOtherMirrorsOnEnter = true;
		public List<MirrorRenderer> mirrorRenderersToEnableOnEnter;

		public List<MirrorRenderer> mirrorRenderersToDisableOnEnter;
		public List<MirrorRenderer> mirrorRenderersToEnableOnLeave;
		public List<MirrorRenderer> mirrorRenderersToDisableOnLeave;


		private void OnTriggerEnter(Collider other)
		{
			if (other.tag == "MainCamera")
			{
				for (int i = 0; i < MirrorRenderer.mirrorRendererInstances.Count; i++)
				{
					MirrorRenderer mr = MirrorRenderer.mirrorRendererInstances[i];
					bool enable = mirrorRenderersToEnableOnEnter.Contains(mr);
					if (enable)
					{
						mr.disableRenderingWhileStillUpdatingMaterials = false;
					}
					else if(disableAllOtherMirrorsOnEnter)
					{
						mr.disableRenderingWhileStillUpdatingMaterials = true;
					}
				}

				mirrorRenderersToDisableOnEnter.ForEach((mr) => {
					mr.disableRenderingWhileStillUpdatingMaterials = true;
				});
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (other.tag == "MainCamera")
			{
				mirrorRenderersToEnableOnLeave.ForEach((mr) => {
					mr.disableRenderingWhileStillUpdatingMaterials = false;
				});
				mirrorRenderersToDisableOnLeave.ForEach((mr) => {
					mr.disableRenderingWhileStillUpdatingMaterials = true;
				});
			}
		}
	}
}
