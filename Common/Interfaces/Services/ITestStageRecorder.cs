using Common.Models.Entities;

namespace Common.Interfaces.Services
{
    public interface ITestStageRecorder
    {
        Dictionary<int, TestStage> TestStages { get; }
        int CurrentStageNumber { get; }
        void AddStage(string action, string input = "", string dataType = "");
        void RecordClientOutput(string data);
        void RecordServerOutput(string data);
        Dictionary<int, TestStage> GetTestStages();
        void DeleteStage(int stageNumber);
        int GetNextStageNumber();
        bool TestKitFolderExists(string testKitName);

    }
}
