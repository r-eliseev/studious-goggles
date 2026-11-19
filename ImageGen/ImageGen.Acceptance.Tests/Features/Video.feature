@video
Feature: Video and audio generation

  @VID-01 @media-generation
  Scenario: Video generation is not offered without a selected video model
    Given the user has a saved OpenRouter key
    And the OpenRouter video model catalog:
      | id            | name       | durations | resolutions | aspect ratios |
      | vendor/video  | Test video | 5         | 720p        | 16:9          |
    And the assistant answers with:
      | response | text             |
      | text     | No video selected |
    When the user creates a new session
    And the user sends the message "Make a short clip"
    Then the last assistant request does not include the "generate_video" tool

  @VID-02 @media-generation
  Scenario: A video job completes and its file is saved
    Given the user has a saved OpenRouter key
    And the OpenRouter video model catalog:
      | id           | name       | durations | resolutions | aspect ratios |
      | vendor/video | Test video | 5         | 720p        | 16:9          |
    And the assistant answers with:
      | response      | prompt           |
      | generate_video | A red kite flying |
    When the user creates a new session
    And the user selects video model "vendor/video"
    And the user sends the message "Make a red kite video"
    Then the assistant request includes the "generate_video" tool
    Then model "vendor/video" has an active video card
    When the provider completes video generation
    Then model "vendor/video" has a done video card with a saved file

  @VID-03 @media-generation
  Scenario: A video download error fails without leaving a partial file
    Given the user has a saved OpenRouter key
    And the OpenRouter video model catalog:
      | id           | name       | durations | resolutions | aspect ratios |
      | vendor/video | Test video | 5         | 720p        | 16:9          |
    And video content downloads fail with HTTP 500
    And the assistant answers with:
      | response      | prompt           |
      | generate_video | A red kite flying |
    When the user creates a new session
    And the user selects video model "vendor/video"
    And the user sends the message "Make a red kite video"
    When the provider completes video generation
    Then model "vendor/video" has failed without a video file

  @VID-04 @media-generation
  Scenario: Cancelling a video warns that the provider may continue
    Given the user has a saved OpenRouter key
    And the OpenRouter video model catalog:
      | id           | name       | durations | resolutions | aspect ratios |
      | vendor/video | Test video | 5         | 720p        | 16:9          |
    And the assistant answers with:
      | response      | prompt           |
      | generate_video | A red kite flying |
    When the user creates a new session
    And the user selects video model "vendor/video"
    And the user sends the message "Make a red kite video"
    When the user cancels the video generation
    Then the video card is cancelled with a provider may continue warning

  @VID-05 @media-generation
  Scenario: Audio generation is offered for a selected audio model and saves a file
    Given the user has a saved OpenRouter key
    And the OpenRouter audio model catalog contains speech model "vendor/audio"
    And the assistant answers with:
      | response      | prompt          |
      | generate_audio | A short chime   |
    When the user creates a new session
    And the user selects audio model "vendor/audio"
    And the user sends the message "Play a short chime"
    Then the assistant request includes the "generate_audio" tool
    And model "vendor/audio" has a done audio card with a saved file

  @VID-06 @media-generation
  Scenario: Audio generation is not offered without a selected audio model
    Given the user has a saved OpenRouter key
    And the OpenRouter audio model catalog contains speech model "vendor/audio"
    And the assistant answers with:
      | response | text            |
      | text     | No audio selected |
    When the user creates a new session
    And the user sends the message "Play a short chime"
    Then the last assistant request does not include the "generate_audio" tool
