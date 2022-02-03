namespace TakeOutMyBloatware.Operations
{
    public interface IActionHandler
    {
        void Run();
        bool IsRebootRecommended => false;
    }
}
