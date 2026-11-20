@gallery
Feature: Gallery, viewing, and comparison

  @GAL-01 @F7.3
  Scenario: The gallery shows only successful pictures of all sessions, newest first
    Given the user has gallery images and failed generations across two sessions
    When the user opens the gallery
    Then the gallery shows only the successful images, newest first

  @GAL-02 @F7.4
  Scenario: The user filters gallery images by model and period
    Given the user has gallery images from different models and dates
    When the user opens the gallery and filters model "vendor/fox" for the last 7 days
    Then the gallery shows only images for model "vendor/fox" from the last 7 days
    When the user changes the gallery period to the last 30 days
    Then the gallery shows only images for model "vendor/fox" from the last 30 days

  @GAL-03 @F7.3
  Scenario: Opening a gallery image opens its source session
    Given the user has an image in a gallery session
    When the user opens that image from the gallery
    Then the source session chat is open

  @GAL-04 @F6.2
  Scenario: The image viewer pages only within one run
    Given a session has a run with two images and another run with an image
    When the user opens the second image in the first run
    Then the viewer contains only the first run images and starts at the selected image
    And viewer paging stops at the first and last image

  @GAL-05 @F6.3
  Scenario: The user compares two images with shared zoom
    Given a session has two successful images to compare
    When the user compares the first image with the second
    Then comparison opens in side-by-side mode
    When the user switches comparison to overlay mode and sets zoom to 3
    Then both comparison images use zoom 3
