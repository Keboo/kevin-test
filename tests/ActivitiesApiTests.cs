using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MergingtonHighSchool.Models;

namespace MergingtonHighSchool.Tests;

public class ActivitiesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ActivitiesApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task GetActivities_ReturnsSuccessAndActivities()
    {
        // Act
        var response = await _client.GetAsync("/api/activities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var activities = await response.Content.ReadFromJsonAsync<Dictionary<string, Activity>>(_jsonOptions);
        activities.Should().NotBeNull();
        activities.Should().NotBeEmpty();
        activities.Should().ContainKey("Chess Club");
        activities.Should().ContainKey("Programming Class");
    }

    [Fact]
    public async Task GetActivities_ContainsExpectedProperties()
    {
        // Act
        var response = await _client.GetAsync("/api/activities");
        var activities = await response.Content.ReadFromJsonAsync<Dictionary<string, Activity>>(_jsonOptions);

        // Assert
        var chessClub = activities!["Chess Club"];
        chessClub.Description.Should().NotBeEmpty();
        chessClub.Schedule.Should().NotBeEmpty();
        chessClub.MaxParticipants.Should().BeGreaterThan(0);
        chessClub.Participants.Should().NotBeNull();
    }

    [Fact]
    public async Task SignupForActivity_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var signupRequest = new SignupRequest
        {
            Email = "test.student@mergington.edu"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/activities/Chess Club/signup", signupRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
        result.Should().ContainKey("message");
        result!["message"].Should().Contain("test.student@mergington.edu");
    }

    [Fact]
    public async Task SignupForActivity_WithNonExistentActivity_ReturnsNotFound()
    {
        // Arrange
        var signupRequest = new SignupRequest
        {
            Email = "student@mergington.edu"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/activities/Nonexistent Activity/signup", signupRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SignupForActivity_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var signupRequest = new SignupRequest
        {
            Email = "duplicate.student@mergington.edu"
        };

        // Act - First signup
        var firstResponse = await _client.PostAsJsonAsync("/api/activities/Programming Class/signup", signupRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Second signup (duplicate)
        var secondResponse = await _client.PostAsJsonAsync("/api/activities/Programming Class/signup", signupRequest);

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await secondResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
        result.Should().ContainKey("detail");
        result!["detail"].Should().Contain("already signed up");
    }

    [Fact]
    public async Task RemoveParticipant_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var signupRequest = new SignupRequest
        {
            Email = "remove.test@mergington.edu"
        };

        // First, signup the participant
        await _client.PostAsJsonAsync("/api/activities/Gym Class/signup", signupRequest);

        // Act - Remove the participant
        var response = await _client.DeleteAsync($"/api/activities/Gym Class/participants/{Uri.EscapeDataString(signupRequest.Email)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
        result.Should().ContainKey("message");
        result!["message"].Should().Contain("Removed");
        result!["message"].Should().Contain(signupRequest.Email);
    }

    [Fact]
    public async Task RemoveParticipant_WithNonExistentActivity_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/activities/Nonexistent Activity/participants/test@mergington.edu");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveParticipant_WithNonExistentParticipant_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/activities/Chess Club/participants/nonexistent@mergington.edu");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions);
        result.Should().ContainKey("detail");
        result!["detail"].Should().Contain("not found");
    }

    [Fact]
    public async Task SignupAndRemove_FullWorkflow_WorksCorrectly()
    {
        // Arrange
        var email = "workflow.test@mergington.edu";
        var activityName = "Basketball Team";
        var signupRequest = new SignupRequest { Email = email };

        // Act & Assert - Get initial state
        var initialResponse = await _client.GetAsync("/api/activities");
        var initialActivities = await initialResponse.Content.ReadFromJsonAsync<Dictionary<string, Activity>>(_jsonOptions);
        var initialCount = initialActivities![activityName].Participants.Count;

        // Act & Assert - Signup
        var signupResponse = await _client.PostAsJsonAsync($"/api/activities/{activityName}/signup", signupRequest);
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify participant was added
        var afterSignupResponse = await _client.GetAsync("/api/activities");
        var afterSignupActivities = await afterSignupResponse.Content.ReadFromJsonAsync<Dictionary<string, Activity>>(_jsonOptions);
        afterSignupActivities![activityName].Participants.Should().Contain(email);
        afterSignupActivities[activityName].Participants.Count.Should().Be(initialCount + 1);

        // Act & Assert - Remove
        var removeResponse = await _client.DeleteAsync($"/api/activities/{Uri.EscapeDataString(activityName)}/participants/{Uri.EscapeDataString(email)}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify participant was removed
        var afterRemoveResponse = await _client.GetAsync("/api/activities");
        var afterRemoveActivities = await afterRemoveResponse.Content.ReadFromJsonAsync<Dictionary<string, Activity>>(_jsonOptions);
        afterRemoveActivities![activityName].Participants.Should().NotContain(email);
        afterRemoveActivities[activityName].Participants.Count.Should().Be(initialCount);
    }
}
