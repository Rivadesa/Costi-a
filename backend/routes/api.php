<?php

declare(strict_types=1);

use App\Http\Controllers\Api\V1\AuthController;
use App\Http\Controllers\Api\V1\ConfigurationController;
use App\Http\Controllers\Api\V1\OperationalController;
use App\Http\Controllers\Api\V1\PreparedTableServiceController;
use App\Http\Controllers\Api\V1\TableServiceController;
use Illuminate\Support\Facades\Route;

Route::prefix('v1')->group(function (): void {
    Route::get('/meta', static fn (): array => [
        'application' => 'Costi-a / Hospitality OS',
        'api_version' => 'v1',
        'phase' => 'V1A',
        'authority' => 'local-primary',
    ]);

    Route::post('/auth/login', [AuthController::class, 'login']);

    Route::middleware('hospitality.auth')->group(function (): void {
        Route::get('/auth/me', [AuthController::class, 'me']);
        Route::post('/auth/logout', [AuthController::class, 'logout']);
        Route::get('/configuration', [ConfigurationController::class, 'show']);

        Route::get('/service-board', [OperationalController::class, 'board'])
            ->middleware('hospitality.permission:service.view');
        Route::get('/kds/stations/{stationId}', [OperationalController::class, 'kds'])
            ->middleware('hospitality.permission:kitchen.view');

        Route::post('/services', [PreparedTableServiceController::class, 'open'])
            ->middleware('hospitality.permission:service.open');
        Route::get('/services/{serviceId}', [TableServiceController::class, 'show'])
            ->middleware('hospitality.permission:service.view');
        Route::post('/services/{serviceId}/menu', [TableServiceController::class, 'assignMenu'])
            ->middleware('hospitality.permission:service.edit');
        Route::post('/services/{serviceId}/guests', [TableServiceController::class, 'addGuest'])
            ->middleware('hospitality.permission:service.edit');
        Route::post('/services/{serviceId}/guests/{guestId}/restrictions', [TableServiceController::class, 'addRestriction'])
            ->middleware('hospitality.permission:service.edit');
        Route::post('/services/{serviceId}/start', [TableServiceController::class, 'start'])
            ->middleware('hospitality.permission:service.manage');
        Route::post('/services/{serviceId}/pause', [TableServiceController::class, 'pause'])
            ->middleware('hospitality.permission:service.manage');
        Route::post('/services/{serviceId}/resume', [TableServiceController::class, 'resume'])
            ->middleware('hospitality.permission:service.manage');

        Route::post('/services/{serviceId}/courses/fire-next', [TableServiceController::class, 'fireNextCourse'])
            ->middleware('hospitality.permission:course.fire');
        Route::post('/services/{serviceId}/courses/{courseId}/ready', [TableServiceController::class, 'validateCourseReady'])
            ->middleware('hospitality.permission:kitchen.pass');
        Route::post('/services/{serviceId}/courses/{courseId}/serve', [TableServiceController::class, 'serveCourse'])
            ->middleware('hospitality.permission:course.serve');
        Route::post('/services/{serviceId}/courses/{courseId}/skip', [TableServiceController::class, 'skipCourse'])
            ->middleware('hospitality.permission:course.modify');
        Route::post('/services/{serviceId}/courses/{courseId}/items/{itemId}/start', [TableServiceController::class, 'startPreparation'])
            ->middleware('hospitality.permission:kitchen.update');
        Route::post('/services/{serviceId}/courses/{courseId}/items/{itemId}/ready', [TableServiceController::class, 'readyPreparation'])
            ->middleware('hospitality.permission:kitchen.update');

        Route::post('/services/{serviceId}/consumptions', [TableServiceController::class, 'addConsumption'])
            ->middleware('hospitality.permission:consumption.add');
        Route::post('/services/{serviceId}/consumptions/{consumptionId}/cancel', [TableServiceController::class, 'cancelConsumption'])
            ->middleware('hospitality.permission:consumption.cancel');
        Route::post('/services/{serviceId}/payments', [TableServiceController::class, 'recordPayment'])
            ->middleware('hospitality.permission:payment.record');
        Route::post('/services/{serviceId}/close', [TableServiceController::class, 'close'])
            ->middleware('hospitality.permission:service.close');
    });
});
