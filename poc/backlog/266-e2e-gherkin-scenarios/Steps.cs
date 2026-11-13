using Allure.NUnit;
using Reqnroll;

namespace ReqnrollAllurePoc;

[Binding]
[AllureNUnit]
public sealed class Steps
{
    [Given("the POC is ready")]
    public void GivenThePocIsReady() { }

    [Then("the scenario passes")]
    public void ThenTheScenarioPasses() { }
}
