namespace MiNET.Items
{
	public abstract class ItemBundleBase : Item
	{
		public ItemBundleBase()
		{
			// Bundles are vanilla items. They used to be hidden while experimental,
			// but must no longer be filtered with Education Edition content.
			Edu = false;
		}
	}
}
