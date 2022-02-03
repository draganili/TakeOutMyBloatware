namespace TakeOutMyBloatware
{
    class RebootHandler
    {
        public bool IsRebootRecommended { private set; get; }

        public void SetRebootRecommended()
        {
            IsRebootRecommended = true;
        }
    }
}
