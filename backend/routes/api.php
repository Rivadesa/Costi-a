<?php

declare(strict_types=1);

use App\Http\Controllers\Api\V1\OperationalController;
use App\Http\Controllers\Api\V1\TableServiceController;
use Illuminate\Support\Facades\Route;

Route::prefix('v1')->group(function (): void {
    Route::get('/meta', static fn (): array => [
        'application' => 'Costi-a / Hospitality OS',
        'api_version' => 'v1',
        'phase' => 'V1A',
        'authority' => 'local-primary',
    ]);

    Route::get('/service-board', [OperationalController::class, 'board']);
    Route::get('/kds/stations/{stationId}', [OperationalController::class, 'kds']);

    Route::post('/services', [TableServiceController::class, 'open']);
    Route::get('/services/{serviceId}', [TableServiceController::class, 'show']);
    Route::post('/services/{serviceId}/menu', [TableServiceController::class, 'assignMenu']);
    Route::post('/services/{serviceId}/guests', [TableServiceController::class, 'addGuest']);
    Route::post('/services/{serviceId}/guests/{guestId}/restrictions', [TableServiceController::class, 'addRestriction']);
    Route::post('/services/{serviceId}/start', [TableServiceController::class, 'start']);
    Route::post('/services/{serviceId}/pause', [TableServiceController::class, 'pause']);
    Route::post('/services/{serviceId}/resume', [TableServiceController::class, 'resume']);

    Route::post('/services/{serviceId}/courses/fire-next', [TableServiceController::class, 'fireNextCourse']);
    Route::post('/services/{serviceId}/courses/{courseId}/ready', [TableServiceController::class, 'validateCourseReady']);
    Route::post('/services/{serviceId}/courses/{courseId}/serve', [TableServiceController::class, 'serveCourse']);
    Route::post('/services/{serviceId}/courses/{courseId}/skip', [TableServiceController::class, 'skipCourse']);
    Route::post('/services/{serviceId}/courses/{courseId}/items/{itemId}/start', [TableServiceController::class, 'startPreparation']);
    Route::post('/services/{serviceId}/courses/{courseId}/items/{itemId}/ready', [TableServiceController::class, 'readyPreparation']);

    Route::post('/services/{serviceId}/consumptions', [TableServiceController::class, 'addConsumption']);
    Route::post('/services/{serviceId}/consumptions/{consumptionId}/cancel', [TableServiceController::class, 'cancelConsumption']);
    Route::post('/services/{serviceId}/payments', [TableServiceController::class, 'recordPayment']);
    Route::post('/services/{serviceId}/close', [TableServiceController::class, 'close']);
});
